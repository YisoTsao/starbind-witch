using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.World.Studio
{
    /// <summary>A biome's static look: tiles per terrain role, the prop catalogue (from the art manifest),
    /// scatter rules, forest density and lighting. Reused by every region of that biome.</summary>
    [CreateAssetMenu(menuName = "Starbind/World Studio/Biome")]
    public sealed class BiomeDefinition : ScriptableObject
    {
        public string id = "moonveil";
        public string displayName = "紫幻森林";

        [Header("Tiles")]
        public TileBase ground;
        public TileBase darkGround;
        public TileBase path;
        public TileBase paver;
        public TileBase water;
        public TileBase cliffFace;
        public TileBase cliffRim;
        public TileBase cliffWaterfall;
        public TileBase cliffStairs;
        [Tooltip("Index = DetailKind - 1")]
        public TileBase[] details = Array.Empty<TileBase>();

        [Header("Props")]
        public PropSpec[] catalog = Array.Empty<PropSpec>();
        public PropRule[] rules = Array.Empty<PropRule>();
        [Tooltip("Forest wall trees: sprite names and weights.")]
        public string[] wallTrees = Array.Empty<string>();
        public float[] wallTreeWeights = Array.Empty<float>();
        [Tooltip("Grid spacing (tiles) of the forest wall trees.")]
        public float treeSpacing = 3f;
        [Tooltip("Chance to keep a tree deeper than 4 tiles inside the forest (fewer objects, canopy still closed).")]
        [Range(0, 1)] public float deepForestKeep = 0.6f;
        public string bridgeSprite = "PROP_mv_bridge";
        public string bridgeVerticalSprite = "PROP_mv_bridge_vertical";
        public string bridgeBrokenSprite = "PROP_mv_bridge_broken";
        public string roadLantern = "PROP_mv_lamp_post";
        [Tooltip("Mechanism sprites (burnable vine strand, its burn frames and charred state; bridge lever off/on).")]
        public string vineSprite = "PROP_mv_vine";
        public string vineBurntSprite = "PROP_mv_vine_burnt";
        public string[] vineBurnFrames = { "PROP_mv_vine_burn_0", "PROP_mv_vine_burn_1", "PROP_mv_vine_burn_2" };
        public string leverSprite = "PROP_mv_lever";
        public string leverOnSprite = "PROP_mv_lever_on";
        public float lanternSpacing = 13f;

        [Header("Ground detail")]
        [Range(0, 1)] public float flowerCoverage = 0.16f;
        [Range(0, 1)] public float cloverChance = 0.05f;

        [Header("Look")]
        public Color globalLight = new Color(0.78f, 0.74f, 0.95f);
        public float globalIntensity = 0.78f;
        public Color background = new Color32(0x12, 0x0e, 0x1e, 0xff);
        [Range(0, 0.5f)] public float characterDim = 0.1f;
        public int ambientMotes = 34;

        Dictionary<string, PropSpec> bySprite;

        public PropSpec Spec(string sprite)
        {
            if (bySprite == null || bySprite.Count != catalog.Length)
            {
                bySprite = new Dictionary<string, PropSpec>();
                foreach (var s in catalog)
                    if (s != null && !string.IsNullOrEmpty(s.sprite))
                        bySprite[s.sprite] = s;
            }
            return sprite != null && bySprite.TryGetValue(sprite, out var spec) ? spec : null;
        }

        void OnValidate() => bySprite = null;
    }
}
