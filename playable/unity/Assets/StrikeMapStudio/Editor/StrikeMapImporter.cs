using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrikeMapStudio.Editor
{
    public static partial class StrikeMapImporter
    {
        private const string OutputRoot = "Assets/StrikeMapStudio/Maps";
        private static readonly HashSet<string> EntityKinds = new HashSet<string> { "floor", "wall", "cover", "platform", "ramp", "decoration" };

        [MenuItem("Tools/StrikeMap Studio/Import map JSON")]
        public static void ImportMenu()
        {
            string input = EditorUtility.OpenFilePanel("Import StrikeMap Studio map", "", "json");
            if (string.IsNullOrEmpty(input)) return;
            try
            {
                string scene = ImportFile(input);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene));
                Debug.Log("[StrikeMap] Imported " + scene + ". Open it to inspect. Use Register selected map only when ready to add it to this game's local catalog.");
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        [MenuItem("Tools/StrikeMap Studio/Register selected map for local UberStrike")]
        public static void RegisterSelectedMenu()
        {
            string scene = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select the imported .unity scene asset in the Project window first.");
            RegisterScene(scene);
        }

        // Batch API: STRIKEMAP_INPUT=/absolute/file.json; STRIKEMAP_RECEIPT=/absolute/receipt.json.
        // Registration is deliberately a separate, explicit API call.
        public static void ImportBatch()
        {
            try
            {
                string scene = ImportFile(Environment.GetEnvironmentVariable("STRIKEMAP_INPUT"));
                string receipt = Environment.GetEnvironmentVariable("STRIKEMAP_RECEIPT");
                if (!string.IsNullOrEmpty(receipt)) File.WriteAllText(receipt, "{\"scene\":\"" + scene + "\",\"imported\":true,\"registered\":false}");
                Debug.Log("[StrikeMap] IMPORT_OK " + scene);
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static string ImportFile(string input)
        {
            if (string.IsNullOrEmpty(input) || !File.Exists(input)) throw new FileNotFoundException("Choose an existing StrikeMap JSON export.", input);
            if (new FileInfo(input).Length > 24 * 1024 * 1024) throw new InvalidDataException("Map exceeds the 24 MiB import limit.");
            string source = File.ReadAllText(input);
            MapDocument map = ReadDocument(source);
            Validate(map);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Save the current untitled Unity scene before importing. StrikeMap preserves open scenes and will not replace unsaved work.");
            EnsureFolder(OutputRoot);
            string slug = Regex.Replace(map.id ?? map.name, "[^a-zA-Z0-9_-]", "_").Trim('_');
            if (slug.Length == 0) slug = "map";
            if (slug.Length > 56) slug = slug.Substring(0, 56);
            string folder = AssetDatabase.GenerateUniqueAssetPath(OutputRoot + "/" + slug);
            EnsureFolder(folder);
            EnsureFolder(folder + "/Materials");
            EnsureFolder(folder + "/Meshes");
            string uniqueName = "StrikeMap_" + Path.GetFileName(folder).Replace(' ', '_');
            string scenePath = folder + "/" + uniqueName + ".unity";
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject(map.name);
                var manifest = root.AddComponent<StrikeMapManifest>();
                manifest.sourceId = map.id; manifest.displayName = map.name; manifest.seed = map.seed;
                manifest.sceneAssetPath = scenePath; manifest.mapId = StableMapId(uniqueName);
                manifest.contractVersion = map.version;
                Bounds authoredBounds = WorldBounds(map);
                manifest.boundsMin = authoredBounds.min; manifest.boundsMax = authoredBounds.max;
                Type configurationType = StrikeMapCatalogAdapter.FindType("MapConfiguration");
                Type spawnType = StrikeMapCatalogAdapter.FindType("SpawnPoint");
                manifest.uberStrikeComponentsFound = configurationType != null && spawnType != null;
                var staticRoot = Child("Geometry", root.transform);
                var spawnsRoot = Child("SpawnPoints", root.transform);
                var pickupsRoot = Child("Pickups", root.transform);
                var materials = new Dictionary<string, Material>();
                var textures = ImportTextureAssets(map, folder);
                var entitiesById = new Dictionary<string, GameObject>();
                foreach (MapEntity entity in map.entities) entitiesById.Add(entity.id, CreateEntity(entity, staticRoot.transform, folder, materials, textures));
                Transform defaultSpawn = null;
                foreach (MapSpawn spawn in map.spawns)
                {
                    var point = Child(spawn.id + "_DM", spawnsRoot.transform);
                    point.transform.position = Vector(spawn.position);
                    point.transform.rotation = Quaternion.Euler(0, spawn.yaw, 0);
                    var marker = point.AddComponent<StrikeMapMarker>();
                    marker.sourceId = spawn.id; marker.kind = "spawn"; marker.team = spawn.team;
                    marker.color = spawn.team == "red" ? Color.red : spawn.team == "blue" ? Color.cyan : Color.green;
                    if (defaultSpawn == null) defaultSpawn = point.transform;
                    AddSpawn(point, spawnType, 101, 0);
                    if (spawn.team != "neutral")
                    {
                        int team = spawn.team == "red" ? 2 : 1;
                        foreach (int mode in new[] { 100, 106 })
                        {
                            var teamPoint = Child(spawn.id + "_" + mode, spawnsRoot.transform);
                            teamPoint.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
                            AddSpawn(teamPoint, spawnType, mode, team);
                        }
                    }
                }
                var pickupsById = new Dictionary<string, GameObject>();
                foreach (MapPickup pickup in map.pickups ?? new MapPickup[0]) pickupsById.Add(pickup.id, CreatePickup(pickup, pickupsRoot.transform, folder, materials));
                CreateDynamics(map, root.transform, entitiesById, pickupsById, defaultSpawn);
                var cameraObject = Child("MapCamera", root.transform);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                Vector3 framingTarget = map.version == "1.0" ? Vector3.zero : authoredBounds.center;
                camera.transform.position = framingTarget + new Vector3(map.size * .55f, map.size * .6f, -map.size * .65f);
                camera.transform.LookAt(framingTarget);
                camera.fieldOfView = 65; camera.farClipPlane = Mathf.Max(500, map.size * 6);
                camera.backgroundColor = ParseColor(map.palette != null ? map.palette.sky : "#172331");
                camera.clearFlags = CameraClearFlags.SolidColor;
                var viewPoint = Child("DefaultViewPoint", root.transform);
                viewPoint.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                var light = Child("Sun", root.transform).AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.42f, .47f, .55f);
                if (manifest.uberStrikeComponentsFound)
                {
                    Component configuration = root.AddComponent(configurationType);
                    SetSerialized(configuration, "_spawnPoints", spawnsRoot);
                    SetSerialized(configuration, "_defaultSpawnPoint", defaultSpawn);
                    SetSerialized(configuration, "_defaultViewPoint", viewPoint.transform);
                    SetSerialized(configuration, "_staticContentParent", staticRoot);
                    SetSerialized(configuration, "_camera", camera);
                    Type environmentType = StrikeMapCatalogAdapter.FindType("LevelEnviroment");
                    Type settingsType = StrikeMapCatalogAdapter.FindType("EnviromentSettings");
                    if (environmentType != null && settingsType != null)
                    {
                        Component environment = root.AddComponent(environmentType);
                        environmentType.GetField("Settings").SetValue(environment, Activator.CreateInstance(settingsType));
                    }
                    Type boundaryType = StrikeMapCatalogAdapter.FindType("LevelBoundary");
                    if (boundaryType != null)
                    {
                        var boundary = Child("LevelBoundary", root.transform);
                        var collider = boundary.AddComponent<BoxCollider>();
                        collider.center = map.version == "1.0" ? new Vector3(0, 20, 0) : authoredBounds.center;
                        collider.size = map.version == "1.0" ? new Vector3(map.size + 8, 50, map.size + 8) : authoredBounds.size + new Vector3(8, 20, 8);
                        collider.isTrigger = true;
                        boundary.AddComponent(boundaryType);
                    }
                }
                Physics.SyncTransforms();
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("Unity did not save the generated scene.");
                File.WriteAllText(folder + "/source.strikemap.json", source);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                Debug.Log("[StrikeMap] " + map.entities.Length + " entities, " + map.spawns.Length + " authored spawns; native game components=" + manifest.uberStrikeComponentsFound);
                return scenePath;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void RegisterScene(string scenePath)
        {
            if (!scenePath.StartsWith(OutputRoot + "/", StringComparison.Ordinal) || !File.Exists(scenePath))
                throw new InvalidOperationException("Only existing generated scenes inside " + OutputRoot + " can be registered.");
            Scene opened = SceneManager.GetSceneByPath(scenePath);
            bool alreadyOpen = opened.IsValid() && opened.isLoaded;
            if (!alreadyOpen) opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                StrikeMapManifest map = opened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StrikeMapManifest>(true)).FirstOrDefault();
                if (map == null || !map.uberStrikeComponentsFound)
                    throw new InvalidOperationException("This scene has no native UberStrike components. Import it inside the UberStrike project before registration.");
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                var entry = new CatalogEntry { sceneName = sceneName, mapId = map.mapId, displayName = map.displayName, description = "Local arena authored in StrikeMap Studio. Seed: " + map.seed };
                string catalogFolder = "Assets/StrikeMapStudio/Resources/StrikeMapStudioCatalog";
                EnsureFolder(catalogFolder);
                string catalogPath = catalogFolder + "/" + sceneName + ".json";
                string json = JsonUtility.ToJson(entry, true);
                if (File.Exists(catalogPath) && File.ReadAllText(catalogPath) != json)
                    throw new IOException("A different registration already exists at " + catalogPath);
                foreach (string path in Directory.GetFiles(catalogFolder, "*.json"))
                {
                    CatalogEntry other = JsonUtility.FromJson<CatalogEntry>(File.ReadAllText(path));
                    if (other.mapId == entry.mapId && other.sceneName != entry.sceneName) throw new InvalidOperationException("Generated map ID collision with " + other.sceneName);
                }
                foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes)
                    if (Path.GetFileNameWithoutExtension(item.path) == sceneName && item.path != scenePath)
                        throw new InvalidOperationException("Another build scene has this name: " + item.path);
                File.WriteAllText(catalogPath, json);
                var scenes = EditorBuildSettings.scenes.ToList();
                int index = scenes.FindIndex(s => s.path == scenePath);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                else scenes[index].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[StrikeMap] Registered " + scenePath + " and " + catalogPath + ". Rebuild the player to include this map. Online servers still need matching map registration.");
            }
            finally { if (!alreadyOpen) EditorSceneManager.CloseScene(opened, true); }
        }

        public static void Validate(MapDocument map)
        {
            if (map == null || (map.version != "1.0" && map.version != "2.0")) throw new InvalidDataException("Expected StrikeMap contract version 1.0 or 2.0.");
            if (string.IsNullOrWhiteSpace(map.id) || string.IsNullOrWhiteSpace(map.name)) throw new InvalidDataException("Map id and name are required.");
            if (!Finite(map.size) || map.size < 8 || map.size > 2048) throw new InvalidDataException("Map size must be between 8 and 2048 metres.");
            if (map.entities == null || map.entities.Length == 0 || map.entities.Length > 10000) throw new InvalidDataException("Expected 1 to 10000 entities.");
            if (map.spawns == null || map.spawns.Length < 2 || map.spawns.Length > 256) throw new InvalidDataException("Expected 2 to 256 spawns.");
            if (map.pickups != null && map.pickups.Length > 1024) throw new InvalidDataException("At most 1024 pickups are supported.");
            var ids = new HashSet<string>();
            foreach (MapEntity e in map.entities)
            {
                if (e == null || !EntityKinds.Contains(e.kind)) throw new InvalidDataException("Unsupported entity kind.");
                Unique(ids, e.id); CheckVector(e.position, "position"); CheckVector(e.size, "size"); CheckVector(e.rotation, "rotation");
                if (e.size.Any(x => x <= 0 || x > 4096)) throw new InvalidDataException("Entity dimensions must be positive and <= 4096.");
                if (e.rotation[0] != 0 || e.rotation[2] != 0) throw new InvalidDataException("Contract v1 supports Y rotation only.");
                ParseColor(e.color);
            }
            foreach (MapSpawn s in map.spawns)
            {
                if (s == null || (s.team != "red" && s.team != "blue" && s.team != "neutral")) throw new InvalidDataException("Invalid spawn team.");
                Unique(ids, s.id); CheckVector(s.position, "spawn position");
                if (!Finite(s.yaw)) throw new InvalidDataException("Spawn yaw must be finite.");
            }
            foreach (MapPickup p in map.pickups ?? new MapPickup[0])
            {
                if (p == null || (p.kind != "health" && p.kind != "armor" && p.kind != "ammo" && !(map.version == "2.0" && (p.kind == "sniper" || p.kind == "heavy-armor")))) throw new InvalidDataException("Invalid pickup kind.");
                Unique(ids, p.id); CheckVector(p.position, "pickup position");
            }
            if (map.version == "2.0") ValidateV2(map);
            ValidateEmbeddedAssets(map.assets);
        }

        private static GameObject CreateEntity(MapEntity e, Transform parent, string folder, Dictionary<string, Material> materials, Dictionary<string, Texture2D> textures)
        {
            GameObject go;
            if (e.kind == "ramp")
            {
                go = Child(e.id, parent);
                Mesh mesh = RampMesh(Vector(e.size));
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/Meshes/ramp.asset"));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                if (e.collidable) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = e.id; go.transform.SetParent(parent, false); go.transform.localScale = Vector(e.size);
                if (e.material != null && !string.IsNullOrEmpty(e.material.texture))
                    go.GetComponent<MeshFilter>().sharedMesh = TexturedBoxMesh(folder, go.GetComponent<MeshFilter>().sharedMesh);
                if (!e.collidable) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            go.transform.SetPositionAndRotation(Vector(e.position), Quaternion.Euler(Vector(e.rotation)));
            go.layer = 0; go.isStatic = true;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(e.color, folder, materials, e.material, textures);
            var marker = go.AddComponent<StrikeMapEntity>();
            marker.sourceId = e.id; marker.kind = e.kind; marker.zone = e.zone; marker.label = e.label; marker.roof = e.roof; marker.walkable = e.walkable;
            return go;
        }

        public static Mesh RampMesh(Vector3 size)
        {
            float x = size.x / 2, y = size.y / 2, z = size.z / 2;
            var corners = new[] { new Vector3(-x,-y,-z), new Vector3(x,-y,-z), new Vector3(-x,-y,z), new Vector3(x,-y,z), new Vector3(-x,y,z), new Vector3(x,y,z) };
            int[] indices = { 0,4,1, 1,4,5, 2,3,4, 3,5,4, 0,1,2, 1,3,2, 0,2,4, 1,5,3 };
            var vertices = new Vector3[indices.Length]; var triangles = new int[indices.Length];
            var uv = new Vector2[indices.Length];
            for (int i = 0; i < indices.Length; i++) { vertices[i] = corners[indices[i]]; triangles[i] = i; uv[i] = new Vector2(vertices[i].x / size.x + .5f, vertices[i].z / size.z + .5f); }
            var mesh = new Mesh { name = "StrikeMap ramp", vertices = vertices, triangles = triangles, uv = uv };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static GameObject CreatePickup(MapPickup pickup, Transform parent, string folder, Dictionary<string, Material> materials)
        {
            var go = Child(pickup.id, parent); go.transform.position = Vector(pickup.position);
            string color = pickup.kind == "health" ? "#68df95" : pickup.kind == "armor" ? "#78bfff" : pickup.kind == "heavy-armor" ? "#ffe18c" : pickup.kind == "sniper" ? "#cd9cff" : "#ffd66e";
            var marker = go.AddComponent<StrikeMapMarker>(); marker.sourceId = pickup.id; marker.kind = pickup.kind; marker.color = ParseColor(color);
            var visual = GameObject.CreatePrimitive(pickup.kind == "ammo" ? PrimitiveType.Cube : PrimitiveType.Sphere);
            visual.name = "Visual"; visual.transform.SetParent(go.transform, false); visual.transform.localScale = Vector3.one * .65f;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.layer = 2; visual.GetComponent<Renderer>().sharedMaterial = MaterialFor(color, folder, materials);
            Type type = StrikeMapCatalogAdapter.FindType(pickup.kind == "health" ? "HealthPickupItem" : (pickup.kind == "armor" || pickup.kind == "heavy-armor") ? "ArmorPickupItem" : pickup.kind == "sniper" ? "WeaponPickupItem" : "AmmoPickupItem");
            if (type == null) return go;
            var collider = go.AddComponent<BoxCollider>(); collider.size = Vector3.one * 1.2f; collider.isTrigger = true;
            Component component = go.AddComponent(type);
            SetSerialized(component, "_pickupItem", visual.transform); SetInteger(component, "_respawnTime", 20);
            if (pickup.kind == "health") SetInteger(component, "_healthPoints", 2); // HP_25
            else if (pickup.kind == "armor") SetInteger(component, "_armorPoints", 1); // Silver / 50
            else if (pickup.kind == "heavy-armor") SetInteger(component, "_armorPoints", 0); // Gold / 100
            else if (pickup.kind == "sniper")
            {
                var field = type.GetField("_weaponType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null || !Enum.IsDefined(field.FieldType, "WeaponSniperRifle")) throw new InvalidOperationException("Native sniper pickup enum is unavailable.");
                SetInteger(component, "_weaponType", Convert.ToInt32(Enum.Parse(field.FieldType, "WeaponSniperRifle")));
                // The real WeaponPickupItem.Start builds the catalog's weapon under this
                // transform. An extra marker renderer would otherwise remain after pickup.
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<MeshRenderer>());
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<MeshFilter>());
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                var field = type.GetField("_ammoType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                int value = field != null && Enum.IsDefined(field.FieldType, "Machinegun") ? Convert.ToInt32(Enum.Parse(field.FieldType, "Machinegun")) : 0;
                SetInteger(component, "_ammoType", value);
            }
            return go;
        }

        private static void AddSpawn(GameObject go, Type type, int mode, int team)
        {
            if (type == null) return;
            Component component = go.AddComponent(type); SetInteger(component, "GameMode", mode); SetInteger(component, "TeamPoint", team);
        }
        private static void SetSerialized(Component target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (property == null) throw new MissingFieldException(target.GetType().Name, field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetInteger(Component target, string field, int value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (property == null) throw new MissingFieldException(target.GetType().Name, field);
            property.intValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material MaterialFor(string hex, string folder, Dictionary<string, Material> materials, MapMaterial settings = null, Dictionary<string, Texture2D> textures = null)
        {
            Material material;
            string key = hex + (settings == null ? "" : JsonUtility.ToJson(settings));
            if (materials.TryGetValue(key, out material)) return material;
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            shader = shader ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No compatible material shader found.");
            material = new Material(shader) { color = ParseColor(hex), name = "Map " + hex };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .18f);
            ApplyMaterialSettings(material, settings, textures);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/Materials/" + hex.TrimStart('#') + ".mat"));
            materials.Add(key, material); return material;
        }
        private static GameObject Child(string name, Transform parent) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static Vector3 Vector(float[] values) { return new Vector3(values[0], values[1], values[2]); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void CheckVector(float[] values, string name)
        {
            if (values == null || values.Length != 3 || values.Any(v => !Finite(v) || Mathf.Abs(v) > 100000)) throw new InvalidDataException("Invalid " + name + "; expected three finite coordinates.");
        }
        private static Color ParseColor(string hex)
        {
            Color color;
            if (string.IsNullOrEmpty(hex) || !Regex.IsMatch(hex, "^#[a-fA-F0-9]{6}$") || !ColorUtility.TryParseHtmlString(hex, out color)) throw new InvalidDataException("Expected #rrggbb color.");
            return color;
        }
        private static void Unique(HashSet<string> ids, string id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Map object IDs must be unique and nonempty."); }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static int StableMapId(string name)
        {
            uint hash = 2166136261;
            foreach (char c in name) hash = unchecked((hash ^ c) * 16777619);
            return 100000 + (int)(hash % 1900000000);
        }
    }
}
