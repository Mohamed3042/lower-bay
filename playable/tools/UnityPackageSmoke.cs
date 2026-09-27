// Copy into Assets/Editor of an empty Unity 6000.0.56f1 project, then execute
// LowerBayPackageSmoke.Begin. This intentionally has no compile-time dependency
// on the package being tested.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LowerBayPackageSmoke
{
    private const string Pending = "LowerBayPackageSmoke.Pending";
    [Serializable] private sealed class Receipt
    {
        public string status = "FAIL", unity, sourceSha256, packageSha256, scene, error;
        public int missingScripts, entities, enabledRenderers, lightmaps;
        public bool sourceMatches, referencesValid;
    }
    public static void Begin()
    {
        SessionState.SetBool(Pending, true);
        SessionState.SetFloat(Pending + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        AssetDatabase.ImportPackage(Environment.GetEnvironmentVariable("LOWER_BAY_PACKAGE"), false);
        EditorApplication.delayCall += Finish;
    }
    [InitializeOnLoadMethod] private static void Resume()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.delayCall += Finish;
    }
    private static void Finish()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Finish; return; }
        var receipt = new Receipt { unity = Application.unityVersion };
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/StrikeMapStudio/Maps") && EditorApplication.timeSinceStartup < SessionState.GetFloat(Pending + ".Deadline", 0))
            { EditorApplication.delayCall += Finish; return; }
            var candidates = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/StrikeMapStudio/Maps" });
            if (candidates.Length == 0 && EditorApplication.timeSinceStartup < SessionState.GetFloat(Pending + ".Deadline", 0))
            { EditorApplication.delayCall += Finish; return; }
            if (candidates.Length != 1) throw new Exception("Expected one packaged scene, found " + candidates.Length);
            receipt.scene = AssetDatabase.GUIDToAssetPath(candidates[0]);
            var scene = EditorSceneManager.OpenScene(receipt.scene);
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            receipt.missingScripts = objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            var behaviours = objects.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c != null).ToArray();
            receipt.entities = behaviours.Count(c => c.GetType().Name == "StrikeMapEntity");
            receipt.enabledRenderers = objects.SelectMany(t => t.GetComponents<Renderer>()).Count(r => r.enabled);
            receipt.lightmaps = LightmapSettings.lightmaps.Length;
            var player = behaviours.Single(c => c.GetType().Name == "LowerBayReviewPlayer");
            var probe = behaviours.Single(c => c.GetType().Name == "LowerBayReviewProbe");
            receipt.referencesValid = new[] { "view", "train", "spawn", "source" }.All(name => player.GetType().GetField(name).GetValue(player) is UnityEngine.Object value && value != null)
                && probe.GetType().GetField("routeFixture").GetValue(probe) is TextAsset;
            var source = (TextAsset)player.GetType().GetField("source").GetValue(player);
            receipt.sourceSha256 = Hash(source.bytes);
            receipt.packageSha256 = Hash(File.ReadAllBytes(Environment.GetEnvironmentVariable("LOWER_BAY_PACKAGE")));
            receipt.sourceMatches = receipt.sourceSha256 == Environment.GetEnvironmentVariable("LOWER_BAY_EXPECTED_SHA");
            if (receipt.missingScripts != 0 || !receipt.referencesValid || !receipt.sourceMatches || receipt.entities != 930 || receipt.enabledRenderers > 300 || receipt.lightmaps == 0)
                throw new Exception("Fresh package import failed its scene contract.");
            receipt.status = "PASS";
        }
        catch (Exception ex) { receipt.error = ex.ToString(); Debug.LogException(ex); }
        SessionState.SetBool(Pending, false);
        File.WriteAllText(Environment.GetEnvironmentVariable("LOWER_BAY_PACKAGE_RECEIPT"), JsonUtility.ToJson(receipt, true));
        Debug.Log("LOWER_BAY_PACKAGE_IMPORT " + receipt.status);
        EditorApplication.Exit(receipt.status == "PASS" ? 0 : 1);
    }
    private static string Hash(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
}
