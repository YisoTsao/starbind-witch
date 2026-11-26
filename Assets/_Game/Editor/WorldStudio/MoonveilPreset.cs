using System;
using System.Collections.Generic;
using System.Linq;
using Starbind.World.Studio;
using UnityEditor;
using UnityEngine;

namespace Starbind.Editor.WorldStudio
{
    /// <summary>Moonveil Forest (紫幻森林) design data for Witch World Studio: biome rules, eleven POI templates
    /// and the 160x120 region blueprint traced from references/01_moonveil_forest_overview.png
    /// (image px -> tile: x / 9.05, y = 120 - y / 9.05). docs/LEVEL_DESIGN.md explains every placement.
    /// Writes ScriptableObjects under Assets/_Game/Data/World/Moonveil/; designers can then edit them in
    /// the Inspector and regenerate from the World Studio window (Apply only overwrites when asked).</summary>
    public static class MoonveilPreset
    {
        public const string Root = "Assets/_Game/Data/World/Moonveil/";
        public const string BiomePath = Root + "BIO_moonveil.asset";
        public const string BlueprintPath = Root + "REG_moonveil_forest.asset";
        public const string ManifestPath = "Assets/_Game/Art/Biomes/Moonveil/moonveil_manifest.json";
        public const string TileFolder = "Assets/_Game/Tiles/Moonveil/";
        public const string AtlasPath = "Assets/_Game/Art/Biomes/Moonveil/ATL_moonveil.spriteatlasv2";

        static string P(string n) => "PROP_mv_" + n;

        static PoiStamp S(string sprite, float x, float y) => new PoiStamp { sprite = P(sprite), offset = new Vector2(x, y) };

        static PoiObject O(string kind, float x, float y, string sprite = null, bool flag = false, string label = null, params string[] lines) =>
            new PoiObject { kind = kind, offset = new Vector2(x, y), sprite = sprite, flag = flag, label = label, lines = lines };

        /// <summary>Imports the art and writes biome, POIs and blueprint. Returns the blueprint.</summary>
        public static RegionBlueprint Apply()
        {
            var art = WorldArtImporter.Import(ManifestPath, TileFolder, AtlasPath);
            var biome = GameContentBuilder.Upsert<BiomeDefinition>(BiomePath, b => FillBiome(b, art));
            var pois = CreatePois();
            var bp = GameContentBuilder.Upsert<RegionBlueprint>(BlueprintPath, r => FillBlueprint(r, biome, pois));
            AssetDatabase.SaveAssets();
            return bp;
        }

        static void FillBiome(BiomeDefinition b, WorldArtImporter.Result art)
        {
            b.id = "moonveil";
            b.displayName = "紫幻森林";
            b.ground = art.ground;
            b.darkGround = art.dark;
            b.path = art.path;
            b.paver = art.paver;
            b.water = art.water;
            b.cliffFace = art.face;
            b.cliffRim = art.rim;
            b.cliffWaterfall = art.waterfall;
            b.cliffStairs = art.stairs;
            b.bridgeVerticalSprite = P("bridge_vertical");
            b.bridgeBrokenSprite = P("bridge_broken");
            b.details = art.details;
            b.catalog = art.catalog;
            b.wallTrees = new[] { P("tree_purple_a"), P("tree_purple_b"), P("tree_wisteria"), P("tree_lantern"), P("tree_pine"), P("tree_blossom"), P("tree_dead") };
            b.wallTreeWeights = new[] { 3f, 3f, 2.2f, 0.5f, 1.3f, 1f, 0.25f };
            b.treeSpacing = 2.7f;
            b.deepForestKeep = 0.75f;
            b.bridgeSprite = P("bridge");
            b.roadLantern = P("lamp_post");
            b.lanternSpacing = 13f;
            b.flowerCoverage = 0.17f;
            b.cloverChance = 0.05f;
            b.globalLight = new Color(0.94f, 0.9f, 1f);
            b.globalIntensity = 1.0f;
            b.background = new Color32(0x12, 0x0e, 0x1e, 0xff);
            b.characterDim = 0.06f;
            b.ambientMotes = 34;
            b.rules = new[]
            {
                new PropRule { id = "edge_undergrowth", habitat = PropHabitat.ForestEdge, density = 70f, spacing = 1.15f,
                    sprites = new[] { P("bush_dark"), P("bush_green"), P("bush_flower"), P("bush_berry"), P("lavender"), P("bellflower"), P("rock_mossy"), P("bush_dark") } },
                new PropRule { id = "clearing_rocks", habitat = PropHabitat.Clearing, density = 2f, spacing = 3f, roadClearance = 1,
                    sprites = new[] { P("rock_large"), P("rock_cluster"), P("rock_mossy"), P("rock_small") } },
                new PropRule { id = "clearing_flowers", habitat = PropHabitat.Clearing, density = 4f, spacing = 2f, roadClearance = 1,
                    sprites = new[] { P("flowers_pink"), P("flowers_violet"), P("lavender"), P("bellflower"), P("bush_flower") },
                    biasKinds = new[] { PoiKind.Gathering, PoiKind.Lake }, biasRadius = 13f, biasBoost = 3f },
                new PropRule { id = "clearing_deadwood", habitat = PropHabitat.Clearing, density = 0.7f, spacing = 4f, roadClearance = 2,
                    sprites = new[] { P("log_hollow"), P("stump_roots") } },
                new PropRule { id = "mushrooms", habitat = PropHabitat.Clearing, density = 0.8f, spacing = 2.2f, roadClearance = 1,
                    sprites = new[] { P("mushroom_pair"), P("mushroom_tall"), P("mushroom_shelf"), P("mushroom_giant") },
                    biasKinds = new[] { PoiKind.MushroomGrove, PoiKind.AncientTree }, biasRadius = 14f, biasBoost = 9f },
                new PropRule { id = "crystals", habitat = PropHabitat.Clearing, density = 1.2f, spacing = 2.4f, roadClearance = 1,
                    sprites = new[] { P("crystal_small"), P("crystal_violet"), P("crystal_blue"), P("crystal_small") },
                    biasKinds = new[] { PoiKind.CrystalGrove }, biasRadius = 13f, biasBoost = 9f, onlyNearBias = true },
                new PropRule { id = "ruins", habitat = PropHabitat.Clearing, density = 0.35f, spacing = 4f, roadClearance = 2,
                    sprites = new[] { P("pillar_short"), P("pillars_broken"), P("pillar_tall") },
                    biasKinds = new[] { PoiKind.Altar, PoiKind.BossArena, PoiKind.CrystalGrove }, biasRadius = 16f, biasBoost = 5f },
                new PropRule { id = "shore", habitat = PropHabitat.Shore, density = 13f, spacing = 2.2f,
                    sprites = new[] { P("reeds"), P("reeds"), P("rock_mossy"), P("bellflower") } },
                new PropRule { id = "lilies", habitat = PropHabitat.WaterSurface, density = 4f, spacing = 3f, sprites = new[] { P("lilypads") } },
            };
        }

        static PoiDefinition Poi(string id, PoiKind kind, string name, Vector2 clearing, Action<PoiDefinition> fill) =>
            GameContentBuilder.Upsert<PoiDefinition>(Root + "POI_" + id + ".asset", p =>
            {
                p.id = id; p.kind = kind; p.displayName = name; p.clearing = clearing;
                p.boundaryNoise = 0.22f; p.ground = GroundStyle.Grass; p.paverRadius = 0; p.safe = false; p.clearCore = 3f; p.roadStop = 0f;
                p.stamps = Array.Empty<PoiStamp>(); p.objects = Array.Empty<PoiObject>();
                fill(p);
            });

        static Dictionary<string, PoiDefinition> CreatePois()
        {
            var d = new Dictionary<string, PoiDefinition>
            {
                ["junction"] = Poi("junction", PoiKind.Junction, "岔路", new Vector2(2.5f, 2.5f), p => p.clearCore = 0f),
                ["entrance"] = Poi("entrance", PoiKind.Entrance, "森林入口", new Vector2(8, 6.5f), p =>
                {
                    p.safe = true; p.clearCore = 4f;
                    p.stamps = new[] { S("pillar_tall", -3.4f, 5.6f), S("pillar_tall", 3.4f, 5.6f), S("lantern_stone", -2.3f, 5.2f), S("lantern_stone", 2.3f, 5.2f),
                                       S("bush_flower", -6f, 1.5f), S("flowers_pink", 6.5f, -1.5f) };
                    p.objects = new[] { O("sign_post", 4.4f, -2.2f, P("signpost"), false, "紫幻森林告示",
                        "北方是月影祭壇；東邊採集林地、西邊有隱藏營地。", "首領空地在東北的懸崖上，只有石階能上去。", "想撤退：按 Esc 選擇放棄本局（材料只保留一半）。") };
                }),
                ["altar"] = Poi("altar", PoiKind.Altar, "月影祭壇", new Vector2(10, 8), p =>
                {
                    p.paverRadius = 2.8f; p.boundaryNoise = 0.26f; p.clearCore = 4.5f; p.roadStop = 2.6f;
                    // roads leave the plaza E (j3), S (j1), W (lake) and NE (ancient tree): ruins only in the NW and SE/SW gaps
                    p.stamps = new[] { S("shrine_moon", -1.5f, 7.2f), S("pillar_tall", -6.2f, 3.6f), S("pillar_short", -6.4f, -4.4f),
                                       S("pillars_broken", 6.8f, -3.6f), S("lantern_stone", -3.6f, 5.4f), S("lantern_stone", 3.6f, 5.4f) };
                    p.objects = new[] { O("altar", 0, 0, P("stone_circle")) };
                }),
                ["ancient"] = Poi("ancient", PoiKind.AncientTree, "古樹密林", new Vector2(9, 6), p =>
                {
                    p.ground = GroundStyle.DarkGrass; p.clearCore = 3.5f;
                    p.stamps = new[] { S("tree_lantern", 0, 2.6f), S("tree_wisteria", -5.5f, 3.4f), S("tree_purple_a", 5.6f, 3.8f), S("mushroom_giant", -3.4f, -2.2f),
                                       S("stump_roots", 4.6f, -2.6f), S("lantern_stone", -1.8f, -3.6f) };
                    p.objects = new[] { O("chest", 2.2f, -1.2f) };
                }),
                ["mushroom"] = Poi("mushroom", PoiKind.MushroomGrove, "蘑菇幽谷", new Vector2(11, 8), p =>
                {
                    p.stamps = new[] { S("mushroom_giant", -4.5f, 2.4f), S("mushroom_giant", 3.6f, 4.2f), S("mushroom_giant", 6.4f, -2.4f), S("mushroom_tall", -6.4f, -3f),
                                       S("mushroom_pair", 0.4f, 5.2f), S("log_hollow", -2.2f, -4.6f) };
                    p.objects = new[] { O("chest", 1.2f, -1.4f) };
                }),
                ["lake"] = Poi("lake", PoiKind.Lake, "幽光湖", new Vector2(17, 10.5f), p => p.clearCore = 0f),
                ["camp"] = Poi("camp", PoiKind.Camp, "隱藏營地", new Vector2(9.5f, 7), p =>
                {
                    p.safe = true; p.clearCore = 5f; p.roadStop = 5f;
                    p.stamps = new[] { S("tent", -2.4f, 2.6f), S("campfire_set", 1.6f, -3.2f), S("merchant_cart", 5.6f, 2.8f), S("lantern_stone", -6.4f, -0.6f),
                                       S("flowers_violet", -5.6f, -3.4f) };
                    p.objects = new[] { O("chest", -4.6f, -2.6f, null, true) };
                }),
                ["gathering"] = Poi("gathering", PoiKind.Gathering, "採集林地", new Vector2(11, 7), p =>
                {
                    p.clearCore = 3f;
                    p.stamps = new[] { S("bush_flower", -5.4f, 3f), S("lavender", -3.2f, -3.2f), S("bellflower", 4.2f, 3.2f), S("flowers_pink", 6.4f, -2.2f),
                                       S("stump_roots", 0.4f, 4.4f), S("bush_berry", -7.4f, -0.4f) };
                    p.objects = new[] { O("chest", 7f, 1.2f) };
                }),
                ["crystal"] = Poi("crystal", PoiKind.CrystalGrove, "水晶林地", new Vector2(10, 8), p =>
                {
                    p.stamps = new[] { S("crystal_violet", -4.2f, 3f), S("crystal_violet", 3.4f, -3.2f), S("crystal_blue", 5.2f, 3.2f), S("crystal_small", -5.4f, -2.2f),
                                       S("crystal_small", 0.2f, 5.2f), S("pillars_broken", -1.4f, 6.2f), S("arch_ruin", 6.2f, 5.6f) };
                }),
                ["boss"] = Poi("boss", PoiKind.BossArena, "首領空地", new Vector2(13, 9), p =>
                {
                    p.safe = true; p.paverRadius = 3.6f; p.clearCore = 8.5f; p.roadStop = 3.6f;
                    p.stamps = new[] { new PoiStamp { sprite = P("magic_circle"), offset = new Vector2(0, 0.4f), ground = true }, S("arch_ruin", 0, 8.2f), S("pillar_tall", -9.4f, 3f), S("pillar_tall", 9.4f, 3f), S("pillar_tall", -7.8f, -4.4f),
                                       S("pillar_tall", 7.8f, -4.4f), S("lantern_stone", -3.2f, 7.4f), S("lantern_stone", 3.2f, 7.4f) };
                    p.objects = new[] { O("boss_arena", 0, 1.2f) };
                }),
                ["waterfall"] = Poi("waterfall", PoiKind.Waterfall, "瀑布潭", new Vector2(6, 4), p => p.clearCore = 2f),
            };
            return d;
        }

        static PoiPlacement Place(string key, PoiDefinition poi, float x, float y, string zone = null) =>
            new PoiPlacement { key = key, poi = poi, position = new Vector2(x, y), zoneName = zone };

        static RoadSpec Road(string from, string to, RoadKind kind, params float[] via)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < via.Length; i += 2)
                pts.Add(new Vector2(via[i], via[i + 1]));
            return new RoadSpec { from = from, to = to, kind = kind, via = pts.ToArray() };
        }

        static Vector2[] Pts(params float[] v) => Enumerable.Range(0, v.Length / 2).Select(i => new Vector2(v[2 * i], v[2 * i + 1])).ToArray();

        static void FillBlueprint(RegionBlueprint r, BiomeDefinition biome, Dictionary<string, PoiDefinition> p)
        {
            r.id = "moonveil_forest";
            r.displayName = "紫幻森林";
            r.sceneName = "MoonveilForest";
            r.chapterId = "forest";
            r.objective = "調查森林深處的異常魔力";
            r.width = 160;
            r.height = 120;
            r.seed = 20261126;
            r.biome = biome;
            r.entryPoi = "entrance";
            r.entrySpawn = new Vector2(77.5f, 3.5f);
            r.entrySafeRadius = 10f;
            r.pois = new[]
            {
                Place("mouth", p["junction"], 77.5f, 1.2f, "森林入口"),
                Place("entrance", p["entrance"], 77.5f, 10f),
                Place("j1", p["junction"], 76f, 33f, "森林小徑"),
                Place("altar", p["altar"], 78f, 61f),
                Place("j3", p["junction"], 115f, 68f, "森林小徑"),
                Place("boss", p["boss"], 129f, 94f),
                Place("camp", p["camp"], 31f, 38f),
                Place("gathering", p["gathering"], 109f, 40f),
                Place("crystal", p["crystal"], 131f, 60f),
                Place("lake", p["lake"], 30f, 68f),
                Place("lakeshore", p["junction"], 40f, 57f, "幽光湖"),
                Place("mushroom", p["mushroom"], 27f, 95f),
                Place("ancient", p["ancient"], 76f, 99f),
                Place("waterfall", p["waterfall"], 101.5f, 75f),
            };
            r.roads = new[]
            {
                Road("mouth", "entrance", RoadKind.Main),
                Road("entrance", "j1", RoadKind.Main, 77f, 22f),
                Road("j1", "altar", RoadKind.Main, 78f, 46f),
                Road("altar", "j3", RoadKind.Main, 88f, 62.5f, 96f, 63.5f, 105f, 64f),
                new RoadSpec { from = "j3", to = "boss", kind = RoadKind.Main, via = Pts(124f, 72f, 130.5f, 76.5f, 130.5f, 85f), meander = 0f },
                Road("j1", "camp", RoadKind.Side, 64f, 35f, 48f, 37.5f),
                Road("j1", "gathering", RoadKind.Side, 90f, 36f, 100f, 38f),
                Road("gathering", "crystal", RoadKind.Side, 118f, 47f, 125f, 53f),
                Road("j3", "crystal", RoadKind.Trail, 123f, 65f),
                // chapter 1 puzzles moved from the old forest: lever bridge over the lake outflow, burnable vine shortcut
                new RoadSpec { from = "altar", to = "lakeshore", kind = RoadKind.Side, via = Pts(68f, 58.5f, 60f, 57.5f, 51f, 57.5f), brokenBridge = true },
                Road("lakeshore", "mushroom", RoadKind.Side, 24f, 57.5f, 15f, 64f, 14f, 77f, 19f, 88f),
                Road("altar", "ancient", RoadKind.Side, 86f, 68f, 84.5f, 79f, 80f, 90f),
                Road("ancient", "mushroom", RoadKind.Trail, 62f, 101f, 46f, 100f, 36f, 97f),
                new RoadSpec { from = "camp", to = "lakeshore", kind = RoadKind.Trail, via = Pts(28f, 46f, 31f, 52f, 36f, 55.5f), vineGateAt = 0.45f },
                Road("j3", "waterfall", RoadKind.Trail, 108f, 72f),
            };
            r.water = new[]
            {
                new WaterSpec { kind = WaterKind.Lake, points = Pts(30f, 68f), size = new Vector2(13f, 6.5f) },
                new WaterSpec { kind = WaterKind.River, points = Pts(42f, 65f, 48f, 61.5f, 52f, 56.5f, 54f, 50f, 56f, 45f), size = new Vector2(2.4f, 0), endPond = 2.8f },
                new WaterSpec { kind = WaterKind.River, points = Pts(113f, 121f, 108f, 106f, 104f, 92f, 103f, 84f, 102f, 77f, 100.5f, 70f, 100f, 63f, 98f, 55f, 95f, 49f),
                                size = new Vector2(2.6f, 0), endPond = 3f },
            };
            r.cliffs = new[]
            {
                new CliffSpec { line = Pts(88f, 82f, 100f, 81f, 112f, 80f, 124f, 79.5f, 136f, 80f, 150f, 81f, 159.5f, 82f),
                                stairs = new[] { new Vector2(130.5f, 5f) }, gateStairs = true },
            };
            r.exits = new[] { new ExitSpec { rect = new Rect(75f, 0f, 5f, 1f), scene = "Village", spawn = "spawn_from_forest", label = "星露村" } };
        }
    }
}
