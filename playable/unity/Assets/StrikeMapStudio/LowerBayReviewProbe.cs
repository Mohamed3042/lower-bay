using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace StrikeMapStudio
{
    // Invoked only with -lowerBayVerify <new-directory>. Exercises the actual
    // packaged player, CharacterController, physics, scene and GPU renderer.
    public sealed class LowerBayReviewProbe : MonoBehaviour
    {
        public TextAsset routeFixture;
        [Serializable] public sealed class RouteResult { public string id, direction; public bool passed; public int completedSegments; public Vector3 end; }
        [Serializable] public sealed class Receipt
        {
            public string status = "FAIL", sourceSha256, unity, graphicsDevice, error;
            public int entities, enabledRenderers, colliders, textures, lightmaps, pickups;
            public bool carry, trappedRiderReset, hazardReset, reset, paused, dropCannotWalkBack, upperFloorClosed;
            public RouteResult[] routes;
            public string[] screenshots;
            public string scope = "Packaged standalone map walkthrough: actual Unity CharacterController routes, moving train carry/crush reset, pickup triggers, GPU frames. No original-game combat, bots or multiplayer acceptance.";
        }
        private LowerBayReviewPlayer player;
        private string folder;
        private Receipt receipt = new Receipt();

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-lowerBayVerify");
            if (arg < 0 || arg + 1 >= args.Length) yield break;
            folder = Path.GetFullPath(args[arg + 1]);
            if (Directory.Exists(folder)) { Debug.LogError("Verification directory already exists."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(folder);
            player = GetComponent<LowerBayReviewPlayer>();
            yield return new WaitForSeconds(.15f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "menu.png"));
            yield return new WaitForSeconds(.15f);
            player.acceptingInput = false;
            yield return null;
            IEnumerator proof = Run();
            while (true)
            {
                object next;
                try { if (!proof.MoveNext()) break; next = proof.Current; }
                catch (Exception ex) { receipt.error = ex.ToString(); break; }
                yield return next;
            }
            File.WriteAllText(Path.Combine(folder, "runtime.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log("LOWER_BAY_RUNTIME " + receipt.status);
            Application.Quit(receipt.status == "PASS" ? 0 : 1);
        }

        private IEnumerator Run()
        {
            var train = player.train;
            train.enabled = false;
            train.ApplyAtTime(0);
            receipt.unity = Application.unityVersion;
            receipt.graphicsDevice = SystemInfo.graphicsDeviceName;
            receipt.entities = FindObjectsByType<StrikeMapEntity>(FindObjectsSortMode.None).Length;
            receipt.colliders = FindObjectsByType<StrikeMapEntity>(FindObjectsSortMode.None).Count(e => e.GetComponent<Collider>() != null);
            receipt.enabledRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled);
            receipt.lightmaps = LightmapSettings.lightmaps.Length;
            receipt.textures = Resources.FindObjectsOfTypeAll<Texture2D>().Count(t => t.name.EndsWith("albedo") || t.name == "lower-bay-sign" || t.name == "winter-poster");
            using (var hash = SHA256.Create()) receipt.sourceSha256 = BitConverter.ToString(hash.ComputeHash(player.source.bytes)).Replace("-", "").ToLowerInvariant();
            // JsonUtility cannot read nested numeric arrays. Routes are compiled
            // to a simple runtime fixture by the Editor from the source JSON.
            var fixture = JsonUtility.FromJson<Fixture>(routeFixture.text);
            var results = new List<RouteResult>();
            foreach (var route in fixture.routes)
            {
                results.Add(Walk(route, false));
                if (route.direction == "both") results.Add(Walk(route, true));
                yield return null;
            }
            receipt.routes = results.ToArray();
            receipt.upperFloorClosed = true;
            foreach (int side in new[] { -1, 1 })
            {
                player.Place(new Vector3(side * 34.65f, 3.42f, 8));
                for (int i = 0; i < 120; i++) player.Drive(Vector2.zero, false, 1f / 60);
                receipt.upperFloorClosed &= player.transform.position.y >= 3.38f;
            }
            receipt.dropCannotWalkBack = true;
            foreach (int side in new[] { -1, 1 })
            {
                player.Place(new Vector3(side * 17, 2.2f, 0));
                for (int i = 0; i < 180; i++) player.Drive(new Vector2(side, 0), false, 1f / 60);
                receipt.dropCannotWalkBack &= player.transform.position.y < 2.7f && Mathf.Abs(player.transform.position.x) < 18;
            }
            player.Place(new Vector3(0, 2.22f, 0));
            for (int i = 0; i < 12; i++) player.Drive(Vector2.zero, false, 1f / 60);
            train.ApplyAtTime(10.5, true);
            receipt.carry = train.CarryCount > 0 && Mathf.Abs(player.transform.position.x - 3.5f) < .08f;
            for (int i = 31; i <= 180; i++)
            {
                train.ApplyAtTime(10 + i / 60.0, true);
                player.Drive(Vector2.zero, false, 1f / 60);
                if (train.TrappedCount > 0) break;
            }
            receipt.trappedRiderReset = train.TrappedCount > 0 && Vector3.Distance(player.transform.position, player.spawn.position) < .2f;
            train.ApplyAtTime(0);
            player.Place(new Vector3(18.9f, -1.2f, 0));
            for (int i = 1; i <= 35; i++) train.ApplyAtTime(10 + i / 60.0, true);
            receipt.hazardReset = train.HazardCount > 0 && Vector3.Distance(player.transform.position, player.spawn.position) < .2f;
            train.Restart();
            receipt.reset = train.transform.position.sqrMagnitude < .000001f;
            train.enabled = true;
            train.Paused = true;
            var stopped = train.transform.position;
            yield return new WaitForSeconds(.12f);
            receipt.paused = Vector3.Distance(stopped, train.transform.position) < .0001f;
            train.enabled = false;
            var pickup = FindObjectsByType<LowerBayReviewPickup>(FindObjectsSortMode.None).First(p => p.GetComponent<StrikeMapMarker>().sourceId == "concourse-armor");
            player.Place(pickup.transform.position - Vector3.up * .8f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            receipt.pickups = player.collected;
            var screenshots = new List<string>();
            foreach (var landmark in fixture.landmarks)
            {
                player.view.transform.SetParent(null, true);
                player.view.transform.position = landmark.position;
                player.view.transform.LookAt(landmark.lookAt);
                yield return null;
                Capture(player.view, Path.Combine(folder, landmark.id + ".png"));
                screenshots.Add(landmark.id + ".png");
            }
            receipt.screenshots = screenshots.ToArray();
            receipt.status = results.All(r => r.passed) && receipt.enabledRenderers <= 300 && receipt.carry && receipt.trappedRiderReset
                && receipt.hazardReset && receipt.reset && receipt.paused && receipt.dropCannotWalkBack && receipt.upperFloorClosed && receipt.pickups > 0 ? "PASS" : "FAIL";
        }

        [Serializable] public sealed class PointRoute { public string id, direction; public Vector3[] points; }
        [Serializable] public sealed class Viewpoint { public string id; public Vector3 position, lookAt; }
        [Serializable] public sealed class Fixture { public PointRoute[] routes; public Viewpoint[] landmarks; }

        private RouteResult Walk(PointRoute route, bool reverse)
        {
            Vector3[] points = reverse ? route.points.Reverse().ToArray() : route.points;
            player.Place(points[0] + Vector3.up * .025f);
            for (int i = 0; i < 15; i++) player.Drive(Vector2.zero, false, 1f / 60);
            var result = new RouteResult { id = route.id, direction = reverse ? "reverse" : "forward" };
            for (int segment = 1; segment < points.Length; segment++)
            {
                Vector3 target = points[segment];
                int frames = Mathf.CeilToInt((Vector3.Distance(player.transform.position, target) / 5 + 8) * 60);
                bool reached = false;
                for (int frame = 0; frame < frames; frame++)
                {
                    Vector3 delta = target - player.transform.position;
                    var direction = new Vector2(delta.x, delta.z);
                    if (direction.magnitude < .12f && Mathf.Abs(delta.y) < .16f) { reached = true; break; }
                    player.Drive(direction.normalized * Mathf.Min(1, direction.magnitude / (5f / 60)), false, 1f / 60);
                }
                if (!reached) break;
                result.completedSegments++;
            }
            result.end = player.transform.position;
            result.passed = result.completedSegments == points.Length - 1;
            return result;
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            var colors = texture.GetPixels32();
            var unique = new HashSet<int>();
            for (int i = 0; i < colors.Length; i += 97) unique.Add((colors[i].r << 16) | (colors[i].g << 8) | colors[i].b);
            if (unique.Count < 100) throw new InvalidOperationException("GPU frame lacks scene detail: " + path);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Destroy(target); Destroy(texture);
        }
    }
}
