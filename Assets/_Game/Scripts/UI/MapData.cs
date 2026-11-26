using System.Collections.Generic;
using Starbind.Player;
using Starbind.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Starbind.UI
{
    /// <summary>A point of interest drawn on the minimap and the world map.</summary>
    public struct MapPoi
    {
        public Transform follow;   // moving NPCs; null for fixed points
        public Vector2 pos;
        public string label;
        public string kind;        // npc, villager, exit, door, star, altar
        public Vector2 Position => follow != null ? (Vector2)follow.position : pos;
    }

    /// <summary>Map picture of the loaded scene, built once per scene from its tilemaps (ground, water,
    /// path, cliff, collision) and big props (trees, buildings, fences), 4 px per cell, plus the points of
    /// interest. Display only: reads the scene, never changes it.</summary>
    public sealed class MapData
    {
        public const int Px = 4;

        public Texture2D Texture { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Scene { get; private set; }
        public readonly List<MapPoi> Pois = new List<MapPoi>();

        static MapData current;
        static Scene currentScene;

        /// <summary>Map of the active scene; rebuilt after a scene change. Null where there is no Grid.</summary>
        public static MapData Current
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                if (current == null || currentScene != scene)
                {
                    current = Build();
                    currentScene = current != null ? scene : default;
                }
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            currentScene = default;
        }

        static Tilemap Find(Grid grid, string name)
        {
            var t = grid.transform.Find(name);
            return t != null ? t.GetComponent<Tilemap>() : null;
        }

        public static MapData Build()
        {
            var grid = Object.FindFirstObjectByType<Grid>();
            var ground = grid != null ? Find(grid, "GroundBase") : null;
            if (ground == null)
                return null;
            var b = ground.cellBounds;
            int W = b.xMax, H = b.yMax;
            if (W <= 0 || H <= 0)
                return null;
            string scene = SceneManager.GetActiveScene().name;
            bool forest = scene == "Forest", interior = scene == "Workshop";
            var biome = BiomeColors(scene);
            var d = new MapData { Width = W, Height = H, Scene = scene };
            var px = new Color32[W * Px * H * Px];
            var water = Find(grid, "Water");
            var path = Find(grid, "Path");
            var cliff = Find(grid, "Cliff");
            var coll = Find(grid, "Collision");
            var dark = Find(grid, "GroundDark");     // World Studio regions: canopy shade / forest floor
            var paver = Find(grid, "Paver");
            Color32 darkC = new Color32(0x2e, 0x24, 0x3e, 255), paverC = new Color32(0xa4, 0x96, 0xb4, 255);

            Color32 grassA = biome.HasValue ? biome.Value.a : forest ? new Color32(0x3f, 0x6e, 0x4a, 255) : interior ? new Color32(0x8e, 0x6a, 0x52, 255) : new Color32(0x74, 0xa2, 0x4e, 255);
            Color32 grassB = biome.HasValue ? biome.Value.b : forest ? new Color32(0x3a, 0x66, 0x45, 255) : interior ? new Color32(0x84, 0x62, 0x4c, 255) : new Color32(0x6c, 0x99, 0x49, 255);
            Color32 pathC = biome.HasValue ? biome.Value.path : forest ? new Color32(0xa0, 0x80, 0x5e, 255) : new Color32(0xdc, 0xae, 0x70, 255);
            Color32 waterC = biome.HasValue ? biome.Value.water : new Color32(0x3a, 0x8a, 0xc4, 255), shore = new Color32(0xa6, 0xdc, 0xea, 255);
            Color32 cliffC = biome.HasValue ? biome.Value.solid : new Color32(0x7a, 0x58, 0x4c, 255), wallC = new Color32(0x3a, 0x2a, 0x40, 255);

            bool Has(Tilemap t, int x, int y) => t != null && t.HasTile(new Vector3Int(x, y, 0));
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool w = Has(water, x, y), p = Has(path, x, y), c = Has(cliff, x, y), solid = Has(coll, x, y);
                    bool dk = Has(dark, x, y), pv = Has(paver, x, y);
                    // plateau rim cells are walkable-looking high ground (grass); only cliff faces are rock
                    var ct = c ? cliff.GetTile(new Vector3Int(x, y, 0)) : null;
                    bool rim = ct != null && ct.name.Contains("rim");
                    if (ct != null && ct.name.Contains("stairs"))
                    {
                        c = false;
                        p = true;
                    }
                    for (int j = 0; j < Px; j++)
                        for (int i = 0; i < Px; i++)
                        {
                            uint h = (uint)((x * Px + i) * 73856093) ^ (uint)((y * Px + j) * 19349663);
                            Color32 col = ((h >> 7) & 3) == 0 ? grassB : grassA;
                            if (dk) col = solid ? darkC : Shade(grassB, 0.78f);
                            if (p) col = ((h >> 9) & 7) == 0 ? Shade(pathC, 0.92f) : pathC;
                            if (pv) col = paverC;
                            if (w)
                            {
                                bool edge = (i == 0 && !Has(water, x - 1, y)) || (i == Px - 1 && !Has(water, x + 1, y)) ||
                                            (j == 0 && !Has(water, x, y - 1)) || (j == Px - 1 && !Has(water, x, y + 1));
                                col = edge ? shore : ((h >> 5) & 15) == 0 ? Shade(waterC, 1.18f) : waterC;
                            }
                            if (rim) col = Shade(col, 1.1f);
                            else if (c) col = j == Px - 1 ? Shade(cliffC, 1.25f) : cliffC;
                            if (solid && !w && !c && (interior || x == 0 || y == 0 || x == W - 1 || y == H - 1))
                                col = wallC;
                            px[(y * Px + j) * W * Px + x * Px + i] = col;
                        }
                }

            // big props: tree canopies, roofs, fences, rocks, fountain
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>())
            {
                if (sr.sprite == null || !sr.enabled)
                    continue;
                string n = sr.sprite.name;
                var bb = sr.bounds;
                if (n.StartsWith("PROP_mv_tree"))
                    Blob(px, W, H, new Vector2(bb.center.x, bb.max.y - bb.extents.x * 0.8f), bb.extents.x * 0.7f, new Color32(0x7a, 0x40, 0xb0, 255));
                else if (n.StartsWith("PROP_mv_crystal"))
                    Blob(px, W, H, bb.center, Mathf.Min(bb.extents.x, 0.6f), new Color32(0xc0, 0x8c, 0xff, 255));
                else if (n.StartsWith("PROP_mv_pillar") || n.StartsWith("PROP_mv_arch") || n.StartsWith("PROP_mv_shrine") || n.StartsWith("PROP_mv_rock_large"))
                    Blob(px, W, H, bb.center, Mathf.Min(bb.extents.x, 0.7f), new Color32(0xa8, 0xa0, 0xbc, 255));
                else if (n.StartsWith("PROP_mv_tent") || n.StartsWith("PROP_mv_merchant"))
                    Box(px, W, H, bb.min.x + 0.1f, bb.min.y + 0.2f, bb.max.x - 0.1f, bb.min.y + bb.size.y * 0.7f, new Color32(0x8a, 0x5c, 0xc9, 255), new Color32(0x5a, 0x38, 0x8a, 255));
                else if (n.StartsWith("PROP_tree_blossom"))
                    Blob(px, W, H, new Vector2(bb.center.x, bb.max.y - bb.extents.x * 0.8f), bb.extents.x * 0.85f, new Color32(0xe8, 0x8c, 0xc4, 255));
                else if (n.StartsWith("PROP_tree") || n.StartsWith("PROP_forest_oak") || n.StartsWith("PROP_forest_pine"))
                    Blob(px, W, H, new Vector2(bb.center.x, bb.max.y - bb.extents.x * 0.8f), bb.extents.x * 0.8f, forest ? new Color32(0x24, 0x4c, 0x34, 255) : new Color32(0x3a, 0x6e, 0x3e, 255));
                else if (n.StartsWith("PROP_forest_dark"))
                    Blob(px, W, H, new Vector2(bb.center.x, bb.max.y - bb.extents.x * 0.8f), bb.extents.x * 0.8f, new Color32(0x3a, 0x44, 0x70, 255));
                else if (n.StartsWith("PROP_cottage") || n.StartsWith("PROP_stall"))
                    Box(px, W, H, bb.min.x + 0.1f, bb.min.y + 0.2f, bb.max.x - 0.1f, bb.min.y + bb.size.y * 0.7f, new Color32(0x8a, 0x5c, 0xc9, 255), new Color32(0x5a, 0x38, 0x8a, 255));
                else if (n.StartsWith("PROP_fence"))
                    Box(px, W, H, bb.min.x, bb.min.y + 0.05f, bb.max.x, bb.min.y + 0.3f, new Color32(0xa8, 0x74, 0x48, 255), new Color32(0x6e, 0x46, 0x2e, 255));
                else if (n.StartsWith("PROP_rock") || n.StartsWith("PROP_pillar"))
                    Blob(px, W, H, bb.center, Mathf.Min(bb.extents.x, 0.5f), new Color32(0xa8, 0xa6, 0xb8, 255));
                else if (n.StartsWith("PROP_crystal_fountain") || n.StartsWith("PROP_starplate") || n.StartsWith("PROP_altar"))
                    Blob(px, W, H, bb.center, Mathf.Min(bb.extents.x, 0.8f), new Color32(0x9a, 0x7c, 0xf0, 255));
                else if (n.StartsWith("PROP_bridge"))
                    Box(px, W, H, bb.min.x, bb.min.y, bb.max.x, bb.max.y, new Color32(0xc8, 0x94, 0x5c, 255), new Color32(0x8a, 0x5c, 0x3a, 255));
                else if (interior && n.StartsWith("INT_") && !n.StartsWith("INT_rug") && !n.StartsWith("INT_window"))
                    Box(px, W, H, bb.min.x, bb.min.y, bb.max.x, bb.min.y + Mathf.Min(bb.size.y, 1.2f), new Color32(0xb0, 0x80, 0x58, 255), new Color32(0x6a, 0x46, 0x30, 255));
            }

            var tex = new Texture2D(W * Px, H * Px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Map_" + scene };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            d.Texture = tex;
            d.CollectPois();
            return d;
        }

        void CollectPois()
        {
            foreach (var t in Object.FindObjectsByType<Talkable>())
            {
                var root = t.transform.parent != null ? t.transform.parent : t.transform;
                bool person = root.GetComponentInChildren<CharacterAnimator>() != null;
                if (!person)
                    continue;
                bool villager = root.GetComponent<NpcWander>() != null;
                Pois.Add(new MapPoi { follow = root, label = ShortName(t.Speaker), kind = villager ? "villager" : "npc" });
            }
            foreach (var e in Object.FindObjectsByType<AreaExit>())
                Pois.Add(new MapPoi { pos = e.Area.center, label = ExitLabel(e.TargetScene), kind = "exit" });
            foreach (var p in Object.FindObjectsByType<DoorPortal>())
                Pois.Add(new MapPoi { pos = p.transform.position, label = "魔女小屋", kind = "door" });
            foreach (var l in Object.FindObjectsByType<LocalTeleport>())
                Pois.Add(new MapPoi { pos = l.transform.position, label = "傳送星盤", kind = "star" });
            foreach (var l in Object.FindObjectsByType<RegionSelector>())
                Pois.Add(new MapPoi { pos = l.transform.position, label = "傳送星盤", kind = "star" });
            foreach (var a in Object.FindObjectsByType<StarAltar>())
                Pois.Add(new MapPoi { pos = a.transform.position, label = "星痕祭壇", kind = "altar" });
            foreach (var s in Object.FindObjectsByType<TrialSeal>())
                Pois.Add(new MapPoi { pos = s.transform.position, label = s.School + "試煉", kind = "altar" });
            var arena = Object.FindAnyObjectByType<BossArena>();
            var run = Starbind.Progression.RunManager.Current;
            if (arena != null && run != null && run.Chapter != null)
                Pois.Add(new MapPoi { pos = arena.Trigger.center, label = run.Chapter.arenaName, kind = "exit" });
        }

        /// <summary>Map colours for the chapter 2-5 biomes (ground A/B, path, water, solid rock/ruin).</summary>
        static (Color32 a, Color32 b, Color32 path, Color32 water, Color32 solid)? BiomeColors(string scene) => scene switch
        {
            "Mine" => (new Color32(0x4a, 0x44, 0x5e, 255), new Color32(0x44, 0x3e, 0x58, 255), new Color32(0x8a, 0x76, 0x60, 255), new Color32(0x22, 0x2a, 0x5a, 255), new Color32(0x2a, 0x24, 0x3a, 255)),
            "Lake" => (new Color32(0x5e, 0x8a, 0x7a, 255), new Color32(0x56, 0x80, 0x72, 255), new Color32(0xb8, 0xc4, 0xd8, 255), new Color32(0x3a, 0x6a, 0xb4, 255), new Color32(0x2c, 0x40, 0x50, 255)),
            "Observatory" => (new Color32(0xe4, 0xec, 0xf4, 255), new Color32(0xd8, 0xe2, 0xee, 255), new Color32(0x9a, 0x94, 0xa4, 255), new Color32(0x8a, 0xb8, 0xdc, 255), new Color32(0x5a, 0x5c, 0x70, 255)),
            "MoonveilForest" => (new Color32(0x52, 0x6e, 0x40, 255), new Color32(0x4c, 0x66, 0x3c, 255), new Color32(0xa8, 0x7c, 0x68, 255), new Color32(0x2c, 0x3c, 0x9a, 255), new Color32(0x5a, 0x4a, 0x6e, 255)),
            "Citadel" => (new Color32(0x3e, 0x30, 0x4e, 255), new Color32(0x38, 0x2a, 0x48, 255), new Color32(0x6e, 0x4e, 0x86, 255), new Color32(0x12, 0x08, 0x1e, 255), new Color32(0x1e, 0x14, 0x2a, 255)),
            _ => null,
        };

        static string ShortName(string speaker)
        {
            if (string.IsNullOrEmpty(speaker))
                return "";
            int i = speaker.IndexOf('（');
            return i > 0 ? speaker.Substring(0, i) : speaker;
        }

        static string ExitLabel(string scene) => scene switch
        {
            "Forest" => "月影森林",
            "MoonveilForest" => "紫幻森林",
            "Village" => "星露村",
            "Workshop" => "魔女小屋",
            "Mine" => "星核礦坑",
            "Lake" => "星輝湖",
            "Observatory" => "失落觀星台",
            "Citadel" => "黑月城塞",
            _ => scene,
        };

        static Color32 Shade(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);

        /// <summary>Shaded disc (lit from the top-left) with a dark rim.</summary>
        static void Blob(Color32[] px, int W, int H, Vector2 c, float r, Color32 col)
        {
            int cx = Mathf.RoundToInt(c.x * Px), cy = Mathf.RoundToInt(c.y * Px), rr = Mathf.Max(1, Mathf.RoundToInt(r * Px));
            for (int y = cy - rr; y <= cy + rr; y++)
                for (int x = cx - rr; x <= cx + rr; x++)
                {
                    if (x < 0 || y < 0 || x >= W * Px || y >= H * Px)
                        continue;
                    float dx = (x - cx) / (float)rr, dy = (y - cy) / (float)rr, d2 = dx * dx + dy * dy;
                    if (d2 > 1f)
                        continue;
                    float lit = -dx * 0.6f + dy * 0.6f;   // texture y grows upward: top-left is (-x, +y)
                    var k = d2 > 0.7f ? 0.7f : lit > 0.35f ? 1.22f : lit < -0.3f ? 0.85f : 1f;
                    px[y * W * Px + x] = Shade(col, k);
                }
        }

        static void Box(Color32[] px, int W, int H, float x0, float y0, float x1, float y1, Color32 fill, Color32 rim)
        {
            int ax = Mathf.RoundToInt(x0 * Px), ay = Mathf.RoundToInt(y0 * Px), bx = Mathf.RoundToInt(x1 * Px), by = Mathf.RoundToInt(y1 * Px);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                    if (x >= 0 && y >= 0 && x < W * Px && y < H * Px)
                        px[y * W * Px + x] = x == ax || x == bx || y == ay || y == by ? rim : fill;
        }
    }
}
