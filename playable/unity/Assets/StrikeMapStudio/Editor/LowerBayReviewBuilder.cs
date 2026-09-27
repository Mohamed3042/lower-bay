using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StrikeMapStudio.Editor
{
    public static class LowerBayReviewBuilder
    {
        [Serializable] private sealed class Receipt
        {
            public string status, sourceSha256, scene, unity, error, visualDirection;
            public int entities, authoredColliders, enabledRenderers, lightmaps, textures;
            public int native4KTextures;
            public bool baked, playerBuilt;
            public string packageSha256;
        }
        private sealed class Batch
        {
            public Transform anchor;
            public Material material;
            public bool moving, roof;
            public readonly List<CombineInstance> parts = new List<CombineInstance>();
            public readonly List<Mesh> temporary = new List<Mesh>();
        }
        private static string PlayableRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));

        [MenuItem("Tools/Lower Bay/Build playable review scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string scene = Build(false);
            EditorSceneManager.OpenScene(scene);
        }

        [MenuItem("Tools/Lower Bay/Build baked 2026 scene")]
        public static void Build2026Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousVersion = Environment.GetEnvironmentVariable("LOWER_BAY_2026");
            string previousBake = Environment.GetEnvironmentVariable("LOWER_BAY_BAKE");
            try
            {
                Environment.SetEnvironmentVariable("LOWER_BAY_2026", "1");
                Environment.SetEnvironmentVariable("LOWER_BAY_BAKE", "1");
                EditorSceneManager.OpenScene(Build(false));
            }
            finally
            {
                Environment.SetEnvironmentVariable("LOWER_BAY_2026", previousVersion);
                Environment.SetEnvironmentVariable("LOWER_BAY_BAKE", previousBake);
            }
        }

        public static void BuildBatch()
        {
            try { Build(true); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void Build2026Batch()
        {
            Environment.SetEnvironmentVariable("LOWER_BAY_2026", "1");
            BuildBatch();
        }

        private static string Build(bool batch)
        {
            string sourcePath = Path.Combine(PlayableRoot, "output/lower-bay.strikemap.json");
            bool realism = Environment.GetEnvironmentVariable("LOWER_BAY_2026") == "1";
            string evidence = Path.Combine(PlayableRoot, realism ? "../reimagine-2026/local/build" : "local/build");
            Directory.CreateDirectory(evidence);
            var result = new Receipt { status = "FAIL", unity = Application.unityVersion, visualDirection = realism ? "2026 realistic station" : "reconstruction blockout" };
            try
            {
                byte[] bytes = File.ReadAllBytes(sourcePath);
                result.sourceSha256 = Hash(bytes);
                var map = StrikeMapImporter.ReadDocument(File.ReadAllText(sourcePath));
                if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/LowerBayBaseline.unity");
                result.scene = StrikeMapImporter.ImportFile(sourcePath);
                var scene = EditorSceneManager.OpenScene(result.scene);
                var root = scene.GetRootGameObjects().Single();
                string folder = Path.GetDirectoryName(result.scene).Replace('\\', '/');
                var entities = root.GetComponentsInChildren<StrikeMapEntity>();
                result.entities = entities.Length;
                result.authoredColliders = entities.Count(e => e.GetComponent<Collider>() != null);
                result.textures = map.assets.Count;
                if (realism) { LowerBayVisual2026.Apply(root, folder); result.native4KTextures = 24; }
                CombineVisuals(root, folder);
                AddLighting(root);
                ConfigureReview(root, folder, sourcePath);
                if (realism) LowerBayVisual2026.ConfigureLighting(root);
                result.enabledRenderers = root.GetComponentsInChildren<Renderer>().Count(r => r.enabled);
                if (result.enabledRenderers > 300) throw new InvalidOperationException("Renderer budget exceeded: " + result.enabledRenderers);
                if (result.authoredColliders != map.entities.Count(e => e.collidable)) throw new InvalidOperationException("Collider count changed during visual batching.");
                EditorSceneManager.SaveScene(scene);
                // Optional bake is explicit: a quick compile never passes itself
                // off as baked. The shipped package's receipt records the result.
                bool bake = Environment.GetEnvironmentVariable("LOWER_BAY_BAKE") == "1";
                if (bake)
                {
                    var settings = new LightingSettings
                    {
                        bakedGI = true, realtimeGI = false,
                        lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                        directSampleCount = realism ? 32 : 16, indirectSampleCount = realism ? 64 : 32,
                        environmentSampleCount = realism ? 32 : 16, lightmapResolution = realism ? 8 : 4,
                        lightmapMaxSize = realism ? 2048 : 1024, lightmapPadding = 4,
                        ao = realism, aoMaxDistance = .8f
                    };
                    AssetDatabase.CreateAsset(settings, folder + "/ReviewLighting.lighting");
                    Lightmapping.lightingSettings = settings;
                    if (!Lightmapping.Bake()) throw new InvalidOperationException("Light bake did not finish successfully.");
                    result.lightmaps = LightmapSettings.lightmaps.Length;
                    result.baked = result.lightmaps > 0;
                    if (!result.baked) throw new InvalidOperationException("Light bake returned no atlases.");
                    LightProbes.Tetrahedralize();
                }
                if (realism) LowerBayVisual2026.BakeReflections(root);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                string package = Path.Combine(PlayableRoot, realism ? "../reimagine-2026/local/LowerBay2026.unitypackage" : "../export/unitypackage/LowerBay_Playable.unitypackage");
                var assets = new List<string> { result.scene };
                assets.AddRange(Directory.GetFiles("Assets/StrikeMapStudio", "*.cs", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')));
                assets.AddRange(Directory.GetFiles("Assets/StrikeMapStudio", "*.asmdef", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')));
                AssetDatabase.ExportPackage(assets.ToArray(), package, ExportPackageOptions.IncludeDependencies);
                result.packageSha256 = Hash(File.ReadAllBytes(package));
                if (batch)
                {
                    string binary = Path.Combine(PlayableRoot, realism ? "builds/LowerBay2026/LowerBay2026.exe" : "builds/LowerBay/LowerBay.exe");
                    Directory.CreateDirectory(Path.GetDirectoryName(binary));
                    PlayerSettings.companyName = "Lower Bay reconstruction";
                    PlayerSettings.productName = realism ? "Lower Bay 2026" : "Lower Bay Review";
                    PlayerSettings.runInBackground = true;
                    PlayerSettings.defaultIsNativeResolution = false;
                    PlayerSettings.defaultScreenWidth = 1280;
                    PlayerSettings.defaultScreenHeight = 720;
                    PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                    PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { result.scene }, locationPathName = binary,
                        target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                    result.playerBuilt = report.summary.result == BuildResult.Succeeded;
                    if (!result.playerBuilt) throw new InvalidOperationException("Windows player build failed: " + report.summary.result);
                }
                result.status = "PASS";
                File.WriteAllText(Path.Combine(evidence, "build.json"), JsonUtility.ToJson(result, true));
                Debug.Log("LOWER_BAY_BUILD_PASS " + JsonUtility.ToJson(result));
                return result.scene;
            }
            catch (Exception ex)
            {
                result.error = ex.ToString();
                File.WriteAllText(Path.Combine(evidence, "build.json"), JsonUtility.ToJson(result, true));
                throw;
            }
        }

        private static void CombineVisuals(GameObject root, string folder)
        {
            AssetDatabase.CreateFolder(folder, "Batched");
            var batches = new Dictionary<string, Batch>();
            foreach (var entity in root.GetComponentsInChildren<StrikeMapEntity>())
            {
                var renderer = entity.GetComponent<MeshRenderer>();
                var filter = entity.GetComponent<MeshFilter>();
                if (renderer == null || filter == null || !renderer.enabled || renderer.forceRenderingOff) continue;
                Material original = renderer.sharedMaterial;
                if (original.renderQueue >= 3000) continue; // Preserve transparent sorting.
                var train = entity.GetComponentInParent<StrikeMapTrain>();
                Transform anchor = train != null ? train.transform : root.transform;
                string signature = original.shader.name + ":" + original.color + ":" + AssetDatabase.GetAssetPath(original.mainTexture)
                    + ":" + original.GetFloat("_Metallic") + ":" + original.GetFloat("_Glossiness") + ":" + original.GetColor("_EmissionColor");
                string key = (train != null ? "train" : "static") + ":" + entity.roof + ":" + Mathf.FloorToInt(entity.transform.position.x / 32) + ":" + signature;
                if (!batches.TryGetValue(key, out var group))
                {
                    var material = new Material(original) { name = "Review surface " + batches.Count };
                    material.mainTextureScale = Vector2.one; material.mainTextureOffset = Vector2.zero;
                    AssetDatabase.CreateAsset(material, folder + "/Batched/surface-" + batches.Count + ".mat");
                    group = new Batch { anchor = anchor, material = material, moving = train != null, roof = entity.roof };
                    batches.Add(key, group);
                }
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                var uv = mesh.uv;
                for (int i = 0; i < uv.Length; i++) uv[i] = Vector2.Scale(uv[i], original.mainTextureScale) + original.mainTextureOffset;
                mesh.uv = uv;
                group.parts.Add(new CombineInstance { mesh = mesh, transform = anchor.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                group.temporary.Add(mesh);
                renderer.enabled = false; // Source geometry and all colliders remain editable.
            }
            int index = 0;
            foreach (var group in batches.Values)
            {
                var mesh = new Mesh { name = "Lower Bay batch " + index, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.parts.ToArray(), true, true);
                if (!group.moving) Unwrapping.GenerateSecondaryUVSet(mesh);
                AssetDatabase.CreateAsset(mesh, folder + "/Batched/mesh-" + index + ".asset");
                foreach (var temporary in group.temporary) UnityEngine.Object.DestroyImmediate(temporary);
                var visual = new GameObject((group.roof ? "RoofVisual_" : "Visual_") + index++);
                visual.transform.SetParent(group.anchor, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.material;
                renderer.lightProbeUsage = group.moving ? LightProbeUsage.BlendProbes : LightProbeUsage.Off;
                if (!group.moving) GameObjectUtility.SetStaticEditorFlags(visual, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic);
            }
        }

        private static void AddLighting(GameObject root)
        {
            var sun = root.transform.Find("Sun").GetComponent<Light>();
            sun.intensity = .22f; sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.38f, .42f, .44f);
            RenderSettings.ambientEquatorColor = new Color(.27f, .30f, .29f);
            RenderSettings.ambientGroundColor = new Color(.17f, .18f, .17f);
            bool bake = Environment.GetEnvironmentVariable("LOWER_BAY_BAKE") == "1";
            var probes = new List<Vector3>();
            foreach (float x in new[] { -30f, -18f, -6f, 6f, 18f, 30f })
                foreach (float z in new[] { -7.4f, -4f, 4f, 10.5f, 14f })
                {
                    float y = z < -6 ? 3.15f : z > 6 ? 3.55f : 4.95f;
                    var host = new GameObject("Station fixture"); host.transform.SetParent(root.transform, false); host.transform.position = new Vector3(x, y, z);
                    var light = host.AddComponent<Light>(); light.type = LightType.Point; light.range = 11; light.intensity = 2.2f;
                    light.color = new Color(.86f, .92f, .85f); light.lightmapBakeType = bake ? LightmapBakeType.Baked : LightmapBakeType.Realtime;
                    light.shadows = bake ? LightShadows.Soft : LightShadows.None;
                    probes.Add(new Vector3(x, .5f, z)); probes.Add(new Vector3(x, 2.2f, z));
                }
            foreach (float x in new[] { -36f, 36f })
                foreach (float z in new[] { 0f, 8f, 14f })
                {
                    var host = new GameObject("Upper fixture"); host.transform.SetParent(root.transform, false); host.transform.position = new Vector3(x, 6.6f, z);
                    var light = host.AddComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 2.4f;
                    light.color = new Color(.9f, .9f, .8f); light.lightmapBakeType = bake ? LightmapBakeType.Baked : LightmapBakeType.Realtime;
                    probes.Add(new Vector3(x, 4f, z)); probes.Add(new Vector3(x, 5.5f, z));
                }
            foreach (float x in new[] { -50f, -36f, -24f, -12f, 0f, 12f, 24f, 36f, 50f })
                foreach (float y in new[] { -.5f, 1f, 3.5f }) probes.Add(new Vector3(x, y, 0));
            foreach (var point in new[] { new Vector3(-36.5f, 2.45f, -9.3f), new Vector3(36.5f, 2.45f, -9.3f),
                new Vector3(-27.5f, 2.35f, 13.4f), new Vector3(-3.5f, 2.3f, -10.8f), new Vector3(3.5f, 2.3f, -10.8f) })
            {
                var host = new GameObject("Room fixture"); host.transform.SetParent(root.transform, false); host.transform.position = point;
                var light = host.AddComponent<Light>(); light.type = LightType.Point; light.range = 5; light.intensity = 1.6f;
                light.color = new Color(.78f, .88f, 1); light.lightmapBakeType = bake ? LightmapBakeType.Baked : LightmapBakeType.Realtime;
                probes.Add(point - Vector3.up);
            }
            var probeHost = new GameObject("Station light probes"); probeHost.transform.SetParent(root.transform, false);
            probeHost.AddComponent<LightProbeGroup>().probePositions = probes.ToArray();
        }

        private static void ConfigureReview(GameObject root, string folder, string source)
        {
            var originalCamera = root.GetComponentInChildren<Camera>(); originalCamera.gameObject.SetActive(false);
            var host = new GameObject("Review player"); host.transform.SetParent(root.transform, false); host.tag = "Player";
            host.transform.SetPositionAndRotation(new Vector3(-27, .02f, 3.4f), Quaternion.Euler(0, 90, 0));
            var capsule = host.AddComponent<CharacterController>(); capsule.height = 2; capsule.radius = .5f;
            capsule.center = Vector3.up; capsule.stepOffset = .4f; capsule.skinWidth = .02f; capsule.slopeLimit = 45;
            var cameraHost = new GameObject("Walkthrough camera"); cameraHost.transform.SetParent(host.transform, false); cameraHost.transform.localPosition = Vector3.up * 1.8f;
            var camera = cameraHost.AddComponent<Camera>(); camera.fieldOfView = 75; camera.nearClipPlane = .04f; camera.farClipPlane = 220;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .034f, .04f); cameraHost.AddComponent<AudioListener>();
            var spawn = new GameObject("Review reset point"); spawn.transform.SetParent(root.transform, false); spawn.transform.SetPositionAndRotation(host.transform.position, host.transform.rotation);
            var player = host.AddComponent<LowerBayReviewPlayer>(); player.view = camera; player.spawn = spawn.transform;
            player.train = root.GetComponentInChildren<StrikeMapTrain>(); player.train.resetSpawn = spawn.transform;
            player.source = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/source.strikemap.json");
            if (player.source == null) throw new InvalidOperationException("Source snapshot was not imported as a TextAsset.");
            var probe = host.AddComponent<LowerBayReviewProbe>();
            foreach (var marker in root.GetComponentsInChildren<StrikeMapMarker>().Where(m => m.kind != "spawn"))
            {
                var trigger = marker.gameObject.AddComponent<SphereCollider>(); trigger.radius = .8f; trigger.isTrigger = true;
                marker.gameObject.AddComponent<LowerBayReviewPickup>();
            }
            if (!AssetDatabase.IsValidFolder("Assets/LowerBayReview")) AssetDatabase.CreateFolder("Assets", "LowerBayReview");
            if (!AssetDatabase.IsValidFolder("Assets/LowerBayReview/Resources")) AssetDatabase.CreateFolder("Assets/LowerBayReview", "Resources");
            var data = StrikeMapJson.Read(File.ReadAllText(source));
            var fixture = new LowerBayReviewProbe.Fixture
            {
                routes = ((List<object>)data["routes"]).Select(item => { var row = (Dictionary<string, object>)item; return new LowerBayReviewProbe.PointRoute
                { id = (string)row["id"], direction = (string)row["direction"], points = ((List<object>)row["points"]).Select(Vector).ToArray() }; }).ToArray(),
                landmarks = ((List<object>)data["landmarks"]).Select(item => { var row = (Dictionary<string, object>)item; return new LowerBayReviewProbe.Viewpoint
                { id = (string)row["id"], position = Vector(row["position"]), lookAt = Vector(row["lookAt"]) }; }).ToArray()
            };
            if (Environment.GetEnvironmentVariable("LOWER_BAY_2026") == "1")
                fixture.landmarks = fixture.landmarks.Concat(new[]
                {
                    new LowerBayReviewProbe.Viewpoint { id="station-sign", position=new Vector3(0,2.1f,2.8f), lookAt=new Vector3(0,3.45f,5.8f) },
                    new LowerBayReviewProbe.Viewpoint { id="bench-and-bin", position=new Vector3(-19.5f,1.45f,12.2f), lookAt=new Vector3(-19.1f,.7f,15.3f) },
                    new LowerBayReviewProbe.Viewpoint { id="vending-detail", position=new Vector3(-3.8f,1.6f,-9.2f), lookAt=new Vector3(-6.2f,1,-10.8f) },
                    new LowerBayReviewProbe.Viewpoint { id="ticket-machines", position=new Vector3(19.5f,1.65f,12.7f), lookAt=new Vector3(19.5f,1.15f,15.2f) },
                    new LowerBayReviewProbe.Viewpoint { id="train-detail", position=new Vector3(-20,1.6f,-3.5f), lookAt=new Vector3(-10,.6f,0) }
                }).ToArray();
            File.WriteAllText("Assets/LowerBayReview/Resources/LowerBayRoutes.json", JsonUtility.ToJson(fixture));
            AssetDatabase.ImportAsset("Assets/LowerBayReview/Resources/LowerBayRoutes.json");
            probe.routeFixture = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/LowerBayReview/Resources/LowerBayRoutes.json");
        }

        private static Vector3 Vector(object value) { var v = (List<object>)value; return new Vector3(Convert.ToSingle(v[0]), Convert.ToSingle(v[1]), Convert.ToSingle(v[2])); }
        private static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
