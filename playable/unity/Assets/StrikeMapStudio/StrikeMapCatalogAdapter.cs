using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StrikeMapStudio
{
    // Optional local catalog integration. Only manifests explicitly registered by the Editor
    // menu are loaded. Existing game source/catalog files are never patched.
    public sealed class StrikeMapCatalogAdapter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindType("MapManager") == null || Resources.LoadAll<TextAsset>("StrikeMapStudioCatalog").Length == 0) return;
            var host = new GameObject("StrikeMap Studio local maps");
            DontDestroyOnLoad(host);
            host.AddComponent<StrikeMapCatalogAdapter>();
        }

        private IEnumerator Start()
        {
            var pause = new WaitForSecondsRealtime(2f);
            while (true)
            {
                try { MergeCatalog(); }
                catch (Exception ex)
                {
                    Debug.LogError("[StrikeMap] Local catalog integration stopped: " + ex.GetBaseException().Message);
                    yield break;
                }
                yield return pause;
            }
        }

        public static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name, false);
                if (type != null) return type;
            }
            return null;
        }

        public static int MergeCatalog()
        {
            Type managerType = FindType("MapManager");
            Type viewType = FindType("UberStrike.Core.Models.Views.MapView");
            if (managerType == null || viewType == null) return 0;
            var instance = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (instance == null) throw new MissingMemberException("MapManager.Instance");
            object manager = instance.GetValue(null, null);
            if ((int)managerType.GetProperty("Count").GetValue(manager, null) < 2) return 0;
            var existing = (IEnumerable)managerType.GetProperty("AllMaps").GetValue(manager, null);
            var views = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(viewType));
            var sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<int>();
            foreach (object map in existing)
            {
                object view = map.GetType().GetProperty("View").GetValue(map, null);
                string scene = (string)viewType.GetProperty("SceneName").GetValue(view, null);
                if (scene == "Menu") continue; // InitializeMapsToLoad recreates the hidden menu itself.
                views.Add(view);
                sceneNames.Add(scene);
                ids.Add((int)viewType.GetProperty("MapId").GetValue(view, null));
            }
            int added = 0;
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("StrikeMapStudioCatalog"))
            {
                CatalogEntry entry = JsonUtility.FromJson<CatalogEntry>(asset.text);
                if (entry == null || entry.version != 1 || string.IsNullOrEmpty(entry.sceneName))
                    throw new FormatException("Invalid local map manifest: " + asset.name);
                if (sceneNames.Contains(entry.sceneName)) continue;
                if (ids.Contains(entry.mapId)) throw new InvalidOperationException("Local map ID collision: " + entry.mapId);
                if (!Application.CanStreamedLevelBeLoaded(entry.sceneName))
                    throw new InvalidOperationException("Registered map is missing from build scenes: " + entry.sceneName);
                object view = Activator.CreateInstance(viewType);
                Set(view, "MapId", entry.mapId);
                Set(view, "SceneName", entry.sceneName);
                Set(view, "DisplayName", entry.displayName);
                Set(view, "Description", entry.description);
                Set(view, "MaxPlayers", Mathf.Clamp(entry.maxPlayers, 2, 16));
                Set(view, "SupportedGameModes", -1);
                Set(view, "SupportedItemClass", -1);
                PropertyInfo settingsProperty = viewType.GetProperty("Settings");
                var settings = (IDictionary)Activator.CreateInstance(settingsProperty.PropertyType);
                Type[] genericTypes = settingsProperty.PropertyType.GetGenericArguments();
                foreach (string mode in new[] { "DeathMatch", "TeamDeathMatch", "EliminationMode" })
                {
                    object settingsValue = Activator.CreateInstance(genericTypes[1]);
                    Set(settingsValue, "KillsMin", 5); Set(settingsValue, "KillsMax", 100); Set(settingsValue, "KillsCurrent", 20);
                    Set(settingsValue, "PlayersMin", 2); Set(settingsValue, "PlayersMax", 16); Set(settingsValue, "PlayersCurrent", 8);
                    Set(settingsValue, "TimeMin", 5); Set(settingsValue, "TimeMax", 30); Set(settingsValue, "TimeCurrent", 10);
                    settings.Add(Enum.Parse(genericTypes[0], mode), settingsValue);
                }
                settingsProperty.SetValue(view, settings, null);
                views.Add(view); sceneNames.Add(entry.sceneName); ids.Add(entry.mapId); added++;
            }
            if (added > 0)
            {
                managerType.GetMethod("InitializeMapsToLoad").Invoke(manager, new object[] { views });
                Debug.Log("[StrikeMap] Added " + added + " explicitly registered local maps.");
            }
            return added;
        }

        private static void Set(object target, string property, object value)
        {
            PropertyInfo member = target.GetType().GetProperty(property);
            if (member == null) throw new MissingMemberException(target.GetType().Name, property);
            member.SetValue(target, value, null);
        }
    }
}
