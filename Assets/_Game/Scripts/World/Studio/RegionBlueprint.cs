using System;
using UnityEngine;

namespace Starbind.World.Studio
{
    /// <summary>Hand-authored macro layout of one region: POI positions, road graph, water and cliffs.
    /// The generator adds the organic shapes, decoration and validation; same seed = same map.</summary>
    [CreateAssetMenu(menuName = "Starbind/World Studio/Region Blueprint")]
    public sealed class RegionBlueprint : ScriptableObject
    {
        public string id = "moonveil_forest";
        public string displayName = "紫幻森林";
        public string sceneName = "MoonveilForest";
        public string chapterId = "forest";
        [TextArea] public string objective = "調查森林深處的異常魔力";
        public int width = 160;
        public int height = 120;
        public int seed = 20261126;
        public BiomeDefinition biome;
        public PoiPlacement[] pois = Array.Empty<PoiPlacement>();
        public RoadSpec[] roads = Array.Empty<RoadSpec>();
        public WaterSpec[] water = Array.Empty<WaterSpec>();
        public CliffSpec[] cliffs = Array.Empty<CliffSpec>();
        public ExitSpec[] exits = Array.Empty<ExitSpec>();
        public string entryPoi = "entrance";
        public Vector2 entrySpawn = new Vector2(77, 3);
        [Tooltip("Keep enemy spawns this far (tiles) from the entry spawn.")]
        public float entrySafeRadius = 10f;
    }
}
