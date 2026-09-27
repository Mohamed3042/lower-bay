using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StrikeMapStudio.Editor
{
    public static partial class StrikeMapImporter
    {
        public static MapDocument ReadDocument(string source)
        {
            Dictionary<string, object> json = StrikeMapJson.Read(source);
            MapDocument map = JsonUtility.FromJson<MapDocument>(source);
            if (map == null) throw new InvalidDataException("Missing map document.");
            map.assets = new Dictionary<string, string>(StringComparer.Ordinal);
            object value;
            if (json.TryGetValue("assets", out value))
            {
                var assets = value as Dictionary<string, object>;
                if (assets == null) throw new InvalidDataException("assets must be an object of embedded image data URLs.");
                foreach (var pair in assets)
                {
                    if (!(pair.Value is string)) throw new InvalidDataException("Embedded asset must be a data URL string.");
                    map.assets.Add(pair.Key, (string)pair.Value);
                }
            }
            var entities = json.ContainsKey("entities") ? json["entities"] as List<object> : null;
            if (map.entities != null && entities != null)
            {
                for (int i = 0; i < map.entities.Length; i++)
                {
                    var raw = entities[i] as Dictionary<string, object>;
                    if (raw == null) throw new InvalidDataException("Entity must be an object.");
                    MapEntity entity = map.entities[i];
                    if (!raw.ContainsKey("walkable")) entity.walkable = entity.kind == "floor" || entity.kind == "platform" || entity.kind == "ramp";
                    if (raw.TryGetValue("material", out value) && value != null)
                    {
                        var material = value as Dictionary<string, object>;
                        if (material == null) throw new InvalidDataException("Entity material must be an object.");
                        if (entity.material == null) entity.material = new MapMaterial();
                        if (!material.ContainsKey("opacity")) entity.material.opacity = 1;
                        if (!material.ContainsKey("roughness")) entity.material.roughness = .82f;
                        if (!material.ContainsKey("emissiveIntensity")) entity.material.emissiveIntensity = 1;
                        if (!material.ContainsKey("uvScale")) entity.material.uvScale = new[] { 1f, 1f };
                    }
                }
            }
            return map;
        }

        public static Bounds WorldBounds(MapDocument map)
        {
            if (map.version == "2.0" && map.bounds != null)
            {
                Vector3 min = Vector(map.bounds.min), max = Vector(map.bounds.max);
                return new Bounds((min + max) / 2, max - min);
            }
            return new Bounds(new Vector3(0, 20, 0), new Vector3(map.size, 50, map.size));
        }

        private static void ValidateV2(MapDocument map)
        {
            if (map.mode != "reconstruction" || map.bounds == null) throw new InvalidDataException("V2 requires reconstruction mode and explicit bounds.");
            CheckVector(map.bounds.min, "bounds.min"); CheckVector(map.bounds.max, "bounds.max");
            Bounds bounds = WorldBounds(map);
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0 || Mathf.Abs(Mathf.Max(bounds.size.x, bounds.size.z) - map.size) > .01f)
                throw new InvalidDataException("V2 bounds must have positive extent and size=max(width,depth).");
            var entities = map.entities.ToDictionary(e => e.id);
            var grouped = new HashSet<string>();
            var motionIds = new HashSet<string>();
            var actorIds = new HashSet<string>((map.pickups ?? new MapPickup[0]).Select(p => p.id));
            var movingActors = new HashSet<string>();
            foreach (MapEntity entity in map.entities)
            {
                float yaw = entity.rotation[1] * Mathf.Deg2Rad;
                Vector3 extent = Vector(entity.size) / 2;
                extent = new Vector3(Mathf.Abs(Mathf.Cos(yaw)) * extent.x + Mathf.Abs(Mathf.Sin(yaw)) * extent.z, extent.y, Mathf.Abs(Mathf.Sin(yaw)) * extent.x + Mathf.Abs(Mathf.Cos(yaw)) * extent.z);
                Vector3 center = Vector(entity.position);
                Bounds padded = bounds; padded.Expand(.02f);
                if (!padded.Contains(center - extent) || !padded.Contains(center + extent)) throw new InvalidDataException("Entity extends outside v2 bounds: " + entity.id);
                MapMaterial material = entity.material;
                if (material == null) continue;
                if (!InRange(material.opacity, 0, 1) || !InRange(material.roughness, 0, 1) || !InRange(material.metalness, 0, 1) || !InRange(material.emissiveIntensity, 0, 5)) throw new InvalidDataException("Invalid material numeric range: " + entity.id);
                if (!string.IsNullOrEmpty(material.emissive)) ParseColor(material.emissive);
                if (material.uvScale == null || material.uvScale.Length != 2 || material.uvScale.Any(v => !InRange(v, .0001f, 10000))) throw new InvalidDataException("uvScale must contain two positive finite numbers.");
                if (!string.IsNullOrEmpty(material.texture) && (map.assets == null || !map.assets.ContainsKey(material.texture))) throw new InvalidDataException("Material references a missing embedded texture: " + material.texture);
            }
            foreach (MapDynamic motion in map.dynamics ?? new MapDynamic[0])
            {
                if (motion == null || string.IsNullOrWhiteSpace(motion.id) || motion.type != "train" || (motion.axis != "x" && motion.axis != "z") || motion.entityIds == null || motion.entityIds.Length == 0)
                    throw new InvalidDataException("Invalid train group.");
                if (!motionIds.Add(motion.id)) throw new InvalidDataException("Duplicate train group ID.");
                if (!Finite(motion.min) || !Finite(motion.max) || motion.max < motion.min || !InRange(motion.speed, 0, 200) || !InRange(motion.pause, 0, 3600) || !Finite(motion.phase)) throw new InvalidDataException("Invalid train timing or offsets.");
                foreach (string id in motion.entityIds)
                {
                    if (!entities.ContainsKey(id) || !grouped.Add(id)) throw new InvalidDataException("Train group contains missing or multiply controlled entity: " + id);
                }
                foreach (string id in motion.actorIds ?? new string[0])
                    if (!actorIds.Contains(id) || !movingActors.Add(id)) throw new InvalidDataException("Train actorIds must reference distinct existing pickups: " + id);
            }
        }

        private static bool InRange(float value, float min, float max) { return Finite(value) && value >= min && value <= max; }

        private static Mesh TexturedBoxMesh(string folder, Mesh primitive)
        {
            string path = folder + "/Meshes/textured-box.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Mesh mesh = UnityEngine.Object.Instantiate(primitive); mesh.name = "StrikeMap upright textured box";
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i], n = normals[i];
                if (Mathf.Abs(n.y) > .5f) uv[i] = new Vector2(p.x + .5f, p.z + .5f);
                else if (Mathf.Abs(n.x) > .5f) uv[i] = new Vector2(n.x > 0 ? p.z + .5f : .5f - p.z, p.y + .5f);
                else uv[i] = new Vector2(n.z > 0 ? .5f - p.x : p.x + .5f, p.y + .5f);
            }
            mesh.uv = uv; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        public static void ValidateEmbeddedAssets(Dictionary<string, string> assets)
        {
            if (assets == null) return;
            if (assets.Count > 64) throw new InvalidDataException("At most 64 embedded textures are supported.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var pair in assets)
            {
                if (!Regex.IsMatch(pair.Key, "^textures/[A-Za-z0-9][A-Za-z0-9_./-]*\\.(png|jpg|jpeg|webp)$", RegexOptions.IgnoreCase)
                    || pair.Key.Split('/').Any(part => part == "." || part == ".." || part.Length == 0) || !names.Add(pair.Key))
                    throw new InvalidDataException("Unsafe or duplicate texture path: " + pair.Key);
                byte[] bytes = DecodeAsset(pair.Key, pair.Value);
                total += bytes.Length;
                if (total > 16 * 1024 * 1024) throw new InvalidDataException("Decoded embedded textures exceed 16 MiB.");
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!ImageConversion.LoadImage(texture, bytes, false)) throw new InvalidDataException("Unity could not decode " + pair.Key + ". Convert WebP to embedded PNG/JPEG before exporting to Unity.");
                    if (texture.width > 4096 || texture.height > 4096) throw new InvalidDataException("Texture dimensions exceed 4096 pixels.");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }

        private static byte[] DecodeAsset(string key, string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 12 * 1024 * 1024) throw new InvalidDataException("Empty or oversized embedded texture.");
            Match match = Regex.Match(value, "^data:image/(png|jpeg|webp);base64,([A-Za-z0-9+/]*={0,2})$", RegexOptions.CultureInvariant);
            if (!match.Success) throw new InvalidDataException("Texture must use a PNG/JPEG/WebP base64 data URL.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(match.Groups[2].Value); } catch (FormatException) { throw new InvalidDataException("Malformed base64 texture."); }
            if (bytes.Length < 12 || bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Texture byte count is invalid.");
            string format = match.Groups[1].Value, extension = Path.GetExtension(key).ToLowerInvariant();
            bool png = format == "png" && extension == ".png" && bytes.Length >= 24 && bytes[0] == 137 && Encoding.ASCII.GetString(bytes, 1, 7) == "PNG\r\n\u001a\n";
            bool jpg = format == "jpeg" && (extension == ".jpg" || extension == ".jpeg") && bytes[0] == 255 && bytes[1] == 216;
            bool webp = format == "webp" && extension == ".webp" && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            if (!png && !jpg && !webp) throw new InvalidDataException("Texture extension, data URL and magic bytes disagree.");
            int width = 0, height = 0;
            if (png) { width = BigEndian(bytes, 16); height = BigEndian(bytes, 20); }
            if (jpg)
            {
                int p = 2;
                while (p + 8 < bytes.Length)
                {
                    if (bytes[p++] != 255) throw new InvalidDataException("Invalid JPEG marker.");
                    while (p < bytes.Length && bytes[p] == 255) p++;
                    int marker = bytes[p++];
                    if (marker == 217 || marker == 218) break;
                    if (marker == 1 || (marker >= 208 && marker <= 215)) continue;
                    int length = (bytes[p] << 8) | bytes[p + 1];
                    if (length < 2 || p + length > bytes.Length) throw new InvalidDataException("Invalid JPEG segment.");
                    if (new[]{192,193,194,195,197,198,199,201,202,203,205,206,207}.Contains(marker)) { height = (bytes[p + 3] << 8) | bytes[p + 4]; width = (bytes[p + 5] << 8) | bytes[p + 6]; break; }
                    p += length;
                }
            }
            if (webp)
            {
                // Reject before decompression when the installed Unity has no WebP codec.
                // Browser export normalization provides portable PNGs without native DLLs.
                throw new InvalidDataException("Unity import requires embedded PNG/JPEG textures; normalize WebP to PNG during export. Original WebP bytes have not been written or changed.");
            }
            if (width < 1 || height < 1 || width > 4096 || height > 4096 || (long)width * height > 16777216) throw new InvalidDataException("Unsupported or oversized texture dimensions.");
            return bytes;
        }

        private static int BigEndian(byte[] bytes, int offset) { return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]; }

        private static Dictionary<string, Texture2D> ImportTextureAssets(MapDocument map, string folder)
        {
            var result = new Dictionary<string, Texture2D>();
            if (map.assets == null || map.assets.Count == 0) return result;
            string fullRoot = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
            foreach (var pair in map.assets)
            {
                string path = folder + "/" + pair.Key;
                if (!Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Texture path escapes generated asset directory.");
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                if (File.Exists(path)) throw new IOException("Generated texture path already exists: " + path);
                File.WriteAllBytes(path, DecodeAsset(pair.Key, pair.Value));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidDataException("Unity did not recognize embedded image: " + path);
                importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) throw new InvalidDataException("Unity texture import failed: " + path);
                result.Add(pair.Key, texture);
            }
            return result;
        }

        private static void ApplyMaterialSettings(Material material, MapMaterial settings, Dictionary<string, Texture2D> textures)
        {
            if (settings == null) return;
            Color color = material.color; color.a = settings.opacity; material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 1 - settings.roughness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1 - settings.roughness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", settings.metalness);
            if (!string.IsNullOrEmpty(settings.texture))
            {
                Texture2D texture;
                if (textures == null || !textures.TryGetValue(settings.texture, out texture)) throw new InvalidDataException("Missing imported texture: " + settings.texture);
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(settings.uvScale[0], settings.uvScale[1]);
                if (material.HasProperty("_BaseMap")) { material.SetTexture("_BaseMap", texture); material.SetTextureScale("_BaseMap", material.mainTextureScale); }
            }
            if (!string.IsNullOrEmpty(settings.emissive))
            {
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", ParseColor(settings.emissive) * settings.emissiveIntensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (settings.opacity < 1)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
                if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.renderQueue = 3000;
            }
        }

        private static void CreateDynamics(MapDocument map, Transform parent, Dictionary<string, GameObject> entities, Dictionary<string, GameObject> pickups, Transform resetSpawn)
        {
            foreach (MapDynamic motion in map.dynamics ?? new MapDynamic[0])
            {
                var group = Child("Train_" + motion.id, parent);
                var body = group.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var train = group.AddComponent<StrikeMapTrain>();
                train.sourceId = motion.id; train.axis = motion.axis; train.min = motion.min; train.max = motion.max; train.speed = motion.speed;
                train.pause = motion.pause; train.phase = motion.phase; train.hazard = motion.hazard; train.resetSpawn = resetSpawn;
                train.authoredOrigin = group.transform.position;
                Type platformType = StrikeMapCatalogAdapter.FindType("MovingPlatform");
                foreach (string id in motion.entityIds)
                {
                    GameObject entity = entities[id]; entity.isStatic = false; entity.transform.SetParent(group.transform, true);
                    Physics.SyncTransforms();
                    Collider solid = entity.GetComponent<Collider>();
                    if (platformType != null && solid != null)
                    {
                        Bounds bounds = solid.bounds;
                        var trigger = Child("Ride_" + id, group.transform);
                        trigger.transform.position = new Vector3(bounds.center.x, bounds.max.y + .2f, bounds.center.z);
                        var top = trigger.AddComponent<BoxCollider>(); top.isTrigger = true; top.size = new Vector3(bounds.size.x, .4f, bounds.size.z);
                        trigger.AddComponent(platformType); train.nativePlatformsConfigured = true;
                    }
                }
                foreach (string id in motion.actorIds ?? new string[0]) pickups[id].transform.SetParent(group.transform, true);
                train.RefreshColliders(); train.ApplyAtTime(0);
            }
        }
    }
}
