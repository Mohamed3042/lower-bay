using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeMapStudio
{
    [Serializable] public sealed class MapDocument
    {
        public string version, id, name, seed, mode, theme;
        public float size;
        public MapPalette palette;
        public MapEntity[] entities;
        public MapSpawn[] spawns;
        public MapPickup[] pickups;
        public MapBounds bounds;
        public MapDynamic[] dynamics;
        [NonSerialized] public Dictionary<string, string> assets;
    }
    [Serializable] public sealed class MapPalette { public string floor, wall, accent, sky; }
    [Serializable] public sealed class MapEntity
    {
        public string id, kind, color;
        public float[] position, size, rotation;
        public bool collidable = true;
        public string zone, label;
        public bool roof, walkable;
        public MapMaterial material;
    }
    [Serializable] public sealed class MapBounds { public float[] min, max; }
    [Serializable] public sealed class MapMaterial
    {
        public float opacity = 1, roughness = .82f, metalness, emissiveIntensity = 1;
        public string emissive, texture;
        public float[] uvScale = new[] { 1f, 1f };
    }
    [Serializable] public sealed class MapDynamic
    {
        public string id, type, axis;
        public string[] entityIds, actorIds;
        public float min, max, speed, pause, phase;
        public bool hazard;
    }
    [Serializable] public sealed class MapSpawn { public string id, team; public float[] position; public float yaw; }
    [Serializable] public sealed class MapPickup { public string id, kind; public float[] position; }
    [Serializable] public sealed class CatalogEntry
    {
        public int version = 1, mapId, maxPlayers = 16;
        public string sceneName, displayName, description;
    }

    public sealed class StrikeMapManifest : MonoBehaviour
    {
        public string sourceId, displayName, seed, sceneAssetPath;
        public int mapId;
        public bool uberStrikeComponentsFound;
        public string contractVersion;
        public Vector3 boundsMin, boundsMax;
    }
}
