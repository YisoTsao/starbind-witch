using System;
using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;

namespace Starbind.World.Studio
{
    /// <summary>Witch World Studio generator (docs/WORLD_GENERATOR_SPEC.md §4). Turns a RegionBlueprint
    /// (macro layout) and its BiomeDefinition (tiles, props, rules) into a WorldMapData. Pure and
    /// deterministic: the same blueprint and seed always give the same map; all randomness comes from
    /// GameRng and WorldNoise seeded from the region seed. Unity objects are only read, never created.</summary>
    public static class WorldGenerator
    {
        public const int Version = 1;

        sealed class Ctx
        {
            public RegionBlueprint bp;
            public BiomeDefinition biome;
            public WorldMapData d;
            public GameRng rng;
            public int seed;
            public int W, H;
            public readonly Dictionary<string, Vector2> poiPos = new Dictionary<string, Vector2>();
            public readonly List<(Vector2 a, Vector2 b)> stairs = new List<(Vector2, Vector2)>();
            public readonly List<RectInt> stairCells = new List<RectInt>();
            public readonly List<Vector2> mainRoadSamples = new List<Vector2>();
            public int[] distForest, distRoad, distWater, distOpen;
            public readonly List<(Vector2 p, float r)> bigProps = new List<(Vector2, float)>();
            public readonly List<(Vector2 p, float r)> smallProps = new List<(Vector2, float)>();
            public int stampCount;
            public readonly List<(int road, Vector2 p, Vector2 tangent)> vineGates = new List<(int, Vector2, Vector2)>();
        }

        public static WorldMapData Generate(RegionBlueprint bp) => Generate(bp, bp.seed);

        public static WorldMapData Generate(RegionBlueprint bp, int seed)
        {
            if (bp == null)
                throw new ArgumentNullException(nameof(bp));
            var c = new Ctx
            {
                bp = bp, biome = bp.biome, seed = seed, W = bp.width, H = bp.height,
                rng = new GameRng(seed), d = new WorldMapData(bp.id, bp.width, bp.height, seed),
            };
            c.d.EntrySpawn = bp.entrySpawn;
            foreach (var p in bp.pois)
                c.poiPos[p.key] = p.position;

            CarveClearings(c);
            CarveWater(c);
            CarveRoads(c);
            FillForestIslands(c, 48);
            ApplyCliffs(c);
            Finalise(c);
            RemovePockets(c);
            c.distForest = Distance(c, i => c.d.Open[i] == 0);
            c.distRoad = Distance(c, i => c.d.Surface[i] != SurfaceCell.None || c.d.Bridge[i] != 0);
            c.distWater = Distance(c, i => c.d.Water[i] != 0);
            PaintGround(c);
            PaintDetail(c);
            PlaceStamps(c);
            PlaceBridges(c);
            if (c.biome != null)
            {
                PlaceLanterns(c);
                PlaceWallTrees(c);
                foreach (var rule in c.biome.rules)
                    Scatter(c, rule);
            }
            PaintSpawn(c);
            BuildObjects(c);
            c.d.Log.Add($"WorldGenerator v{Version} {bp.id} seed {seed}: {c.W}x{c.H}, props {c.d.Props.Count} (stamps {c.stampCount}), objects {c.d.Objects.Count}");
            return c.d;
        }

        // ------------------------------------------------------------------ shapes
        static void Blob(Ctx c, Vector2 centre, Vector2 radius, float noise, int salt, Action<int, int, float> fill)
        {
            int x0 = Mathf.FloorToInt(centre.x - radius.x * (1 + noise)) - 1, x1 = Mathf.CeilToInt(centre.x + radius.x * (1 + noise)) + 1;
            int y0 = Mathf.FloorToInt(centre.y - radius.y * (1 + noise)) - 1, y1 = Mathf.CeilToInt(centre.y + radius.y * (1 + noise)) + 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!c.d.In(x, y))
                        continue;
                    float dx = (x + 0.5f - centre.x) / Mathf.Max(0.5f, radius.x), dy = (y + 0.5f - centre.y) / Mathf.Max(0.5f, radius.y);
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = 1f + noise * WorldNoise.Fbm(Mathf.Cos(ang) * 1.7f + salt * 3.1f, Mathf.Sin(ang) * 1.7f, c.seed + salt, 2);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    if (r <= 1f)
                        fill(x, y, r);
                }
        }

        static void Disk(Ctx c, Vector2 p, float r, Action<int, int> fill)
        {
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.CeilToInt(p.x + r), y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.CeilToInt(p.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (c.d.In(x, y) && (x + 0.5f - p.x) * (x + 0.5f - p.x) + (y + 0.5f - p.y) * (y + 0.5f - p.y) <= r * r)
                        fill(x, y);
        }

        /// <summary>Catmull-Rom through the control points, sampled about every `step` tiles, then pushed
        /// sideways by noise (tapered to zero at both ends so roads still meet their POIs).</summary>
        public static List<Vector2> Spline(IReadOnlyList<Vector2> pts, float step, float meander, int salt, int seed)
        {
            var raw = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    raw.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            raw.Add(pts[pts.Count - 1]);
            if (meander <= 0 || raw.Count < 3)
                return raw;
            float total = 0;
            var s = new float[raw.Count];
            for (int i = 1; i < raw.Count; i++)
                s[i] = total += Vector2.Distance(raw[i - 1], raw[i]);
            var outPts = new List<Vector2>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                var tan = (raw[Mathf.Min(raw.Count - 1, i + 1)] - raw[Mathf.Max(0, i - 1)]).normalized;
                var nrm = new Vector2(-tan.y, tan.x);
                float taper = Mathf.Clamp01(Mathf.Min(s[i], total - s[i]) / 6f);
                outPts.Add(raw[i] + nrm * (meander * taper * WorldNoise.Fbm(s[i] * 0.07f, salt * 5.3f, seed + 101, 2)));
            }
            return outPts;
        }

        // ------------------------------------------------------------------ passes
        static void CarveClearings(Ctx c)
        {
            for (int i = 0; i < c.bp.pois.Length; i++)
            {
                var pl = c.bp.pois[i];
                var def = pl.poi;
                if (def == null)
                    continue;
                byte zone = (byte)(i + 1);
                Blob(c, pl.position, def.clearing, def.boundaryNoise, i + 1, (x, y, r) =>
                {
                    int k = c.d.Index(x, y);
                    c.d.Open[k] = 1;
                    if (c.d.Zone[k] == 0)
                        c.d.Zone[k] = zone;
                    if (def.ground == GroundStyle.DarkGrass)
                        c.d.Ground[k] = 1;
                });
                if (def.paverRadius > 0)
                    Blob(c, pl.position, new Vector2(def.paverRadius, def.paverRadius * 0.8f), 0.12f, 50 + i,
                        (x, y, r) => c.d.Surface[c.d.Index(x, y)] = SurfaceCell.Paver);
                c.d.Pois.Add(new PoiInstance
                {
                    key = pl.key, kind = def.kind, displayName = string.IsNullOrEmpty(def.displayName) ? pl.key : def.displayName,
                    zoneName = string.IsNullOrEmpty(pl.zoneName) ? def.displayName : pl.zoneName, center = pl.position,
                    radius = def.clearing, safe = def.safe,
                });
            }
        }

        static void CarveWater(Ctx c)
        {
            int salt = 200;
            foreach (var w in c.bp.water)
            {
                salt += 17;
                if (w.points == null || w.points.Length == 0)
                    continue;
                if (w.kind == WaterKind.Lake)
                {
                    var centre = w.points[0];
                    Blob(c, centre, w.size + new Vector2(2.5f, 2f), 0.18f, salt, (x, y, r) => c.d.Open[c.d.Index(x, y)] = 1);
                    Blob(c, centre, w.size, 0.2f, salt, (x, y, r) => c.d.Water[c.d.Index(x, y)] = 1);
                    continue;
                }
                var line = Spline(w.points, 0.3f, 2.2f, salt, c.seed);
                float half = Mathf.Max(0.6f, w.size.x * 0.5f);
                for (int i = 0; i < line.Count; i++)
                {
                    float r = half * (1f + 0.18f * WorldNoise.Value(i * 0.05f, salt, c.seed + 7));
                    Disk(c, line[i], r + 1.6f, (x, y) => c.d.Open[c.d.Index(x, y)] = 1);
                    Disk(c, line[i], r, (x, y) => c.d.Water[c.d.Index(x, y)] = 1);
                }
                if (w.endPond > 0)
                {
                    var end = line[line.Count - 1];
                    Blob(c, end, new Vector2(w.endPond + 2f, w.endPond * 0.8f + 2f), 0.15f, salt + 1, (x, y, r) => c.d.Open[c.d.Index(x, y)] = 1);
                    Blob(c, end, new Vector2(w.endPond, w.endPond * 0.8f), 0.2f, salt + 1, (x, y, r) => c.d.Water[c.d.Index(x, y)] = 1);
                }
            }
        }

        static void CarveRoads(Ctx c)
        {
            int salt = 400;
            for (int ri = 0; ri < c.bp.roads.Length; ri++)
            {
                var road = c.bp.roads[ri];
                byte tag = (byte)Mathf.Min(255, ri + 1);
                salt += 11;
                if (!c.poiPos.TryGetValue(road.from, out var a) || !c.poiPos.TryGetValue(road.to, out var b))
                {
                    c.d.Log.Add($"road {road.from}->{road.to}: unknown POI key");
                    continue;
                }
                var pts = new List<Vector2> { a };
                pts.AddRange(road.via ?? Array.Empty<Vector2>());
                pts.Add(b);
                float width = road.width > 0 ? road.width : road.kind == RoadKind.Main ? 3f : road.kind == RoadKind.Side ? 2.2f : 1.6f;
                float meander = road.meander >= 0 ? road.meander : road.kind == RoadKind.Main ? 1.2f : 1.8f;
                float margin = road.kind == RoadKind.Main ? 2.2f : 1.6f;
                var line = Spline(pts, 0.25f, meander, salt, c.seed);
                // the road enters a POI's plaza but does not cross its core (stamps ring the core)
                float coreA = CoreOf(c, road.from), coreB = CoreOf(c, road.to);
                for (int i = 0; i < line.Count; i++)
                {
                    var p = line[i];
                    bool inCore = (p - a).sqrMagnitude < coreA * coreA || (p - b).sqrMagnitude < coreB * coreB;
                    float n = WorldNoise.Value(i * 0.06f, salt, c.seed + 9);
                    float half = width * 0.5f * (1f + 0.12f * n);
                    Disk(c, p, half + margin + 0.9f * (WorldNoise.Value(i * 0.04f, salt + 1, c.seed) + 1f), (x, y) => c.d.Open[c.d.Index(x, y)] = 1);
                    if (inCore)
                        continue;
                    Disk(c, p, half, (x, y) =>
                    {
                        int k = c.d.Index(x, y);
                        if (c.d.Water[k] != 0)
                        {
                            if (c.d.Bridge[k] == 0)
                                c.d.Bridge[k] = tag; // remembers its road (broken bridge option)
                        }
                        else if (c.d.Surface[k] == SurfaceCell.None)
                            c.d.Surface[k] = SurfaceCell.Path;
                    });
                    if (road.kind == RoadKind.Main && i % 4 == 0)
                        c.mainRoadSamples.Add(p);
                }
                if (road.vineGateAt > 0 && line.Count > 2)
                {
                    int gi = Mathf.Clamp(Mathf.RoundToInt(road.vineGateAt * (line.Count - 1)), 1, line.Count - 2);
                    c.vineGates.Add((ri, line[gi], (line[gi + 1] - line[gi - 1]).normalized));
                }
            }
        }

        /// <summary>Forest patches smaller than `maxCells` left between carved shapes are too small for a tree
        /// and would read as dark blots with invisible collision: they become open grass.</summary>
        static void FillForestIslands(Ctx c, int maxCells)
        {
            var d = c.d;
            var seen = new bool[c.W * c.H];
            var comp = new List<int>();
            int filled = 0;
            for (int i = 0; i < seen.Length; i++)
            {
                if (seen[i] || d.Open[i] != 0)
                    continue;
                comp.Clear();
                bool border = false;
                var q = new Queue<int>();
                q.Enqueue(i);
                seen[i] = true;
                while (q.Count > 0)
                {
                    int k = q.Dequeue(), x = k % c.W, y = k / c.W;
                    comp.Add(k);
                    if (x == 0 || y == 0 || x == c.W - 1 || y == c.H - 1)
                        border = true;
                    if (x > 0 && !seen[k - 1] && d.Open[k - 1] == 0) { seen[k - 1] = true; q.Enqueue(k - 1); }
                    if (x < c.W - 1 && !seen[k + 1] && d.Open[k + 1] == 0) { seen[k + 1] = true; q.Enqueue(k + 1); }
                    if (y > 0 && !seen[k - c.W] && d.Open[k - c.W] == 0) { seen[k - c.W] = true; q.Enqueue(k - c.W); }
                    if (y < c.H - 1 && !seen[k + c.W] && d.Open[k + c.W] == 0) { seen[k + c.W] = true; q.Enqueue(k + c.W); }
                }
                if (border || comp.Count >= maxCells)
                    continue;
                foreach (int k in comp)
                    d.Open[k] = 1;
                filled += comp.Count;
            }
            if (filled > 0)
                d.Log.Add($"islands: {filled} cells of tiny forest patches opened");
        }

        static float CoreOf(Ctx c, string key)
        {
            var def = FindPoi(c, key);
            return def != null ? def.roadStop : 0f;
        }

        static float LineY(Vector2[] line, float x)
        {
            if (x <= line[0].x)
                return line[0].y;
            for (int i = 0; i < line.Length - 1; i++)
                if (x <= line[i + 1].x)
                    return Mathf.Lerp(line[i].y, line[i + 1].y, (x - line[i].x) / Mathf.Max(0.001f, line[i + 1].x - line[i].x));
            return line[line.Length - 1].y;
        }

        static void ApplyCliffs(Ctx c)
        {
            foreach (var cl in c.bp.cliffs)
            {
                if (cl.line == null || cl.line.Length < 2)
                    continue;
                int xa = Mathf.Max(0, Mathf.FloorToInt(cl.line[0].x)), xb = Mathf.Min(c.W - 1, Mathf.CeilToInt(cl.line[cl.line.Length - 1].x));
                bool gated = false;
                for (int x = xa; x <= xb; x++)
                {
                    // integer steps every few tiles keep the face rows readable (no 1-tile zigzag)
                    float jag = WorldNoise.Value(x * 0.18f, 3.3f, c.seed + 31) * 1.2f;
                    int yl = Mathf.RoundToInt(LineY(cl.line, x + 0.5f) + jag);
                    bool stair = false;
                    foreach (var s in cl.stairs)
                        if (Mathf.Abs(x + 0.5f - s.x) <= s.y * 0.5f)
                            stair = true;
                    for (int y = yl; y < c.H; y++)
                        if (c.d.In(x, y))
                            c.d.Plateau[c.d.Index(x, y)] = 1;
                    for (int y = yl + 2; y < c.H; y++)
                        c.d.Cliff[c.d.Index(x, y)] = CliffCell.Rim;
                    for (int y = yl; y <= yl + 1 && y < c.H; y++)
                    {
                        int k = c.d.Index(x, y);
                        if (stair)
                        {
                            c.d.Cliff[k] = CliffCell.Stairs;
                            c.d.Open[k] = 1;
                            c.d.Water[k] = 0;
                            c.d.Surface[k] = SurfaceCell.None;
                            continue;
                        }
                        bool fall = c.d.Water[k] != 0 || c.d.Get(c.d.Water, x, y + 2) != 0 && c.d.Get(c.d.Water, x, y - 1) != 0;
                        c.d.Cliff[k] = fall ? CliffCell.Waterfall : CliffCell.Face;
                        c.d.Water[k] = 0;
                        c.d.Surface[k] = SurfaceCell.None;
                        c.d.Bridge[k] = 0;
                    }
                }
                // stair records: gate the first stair's upper row when asked
                foreach (var s in cl.stairs)
                {
                    int yl = Mathf.RoundToInt(LineY(cl.line, s.x) + WorldNoise.Value(Mathf.Floor(s.x) * 0.18f, 3.3f, c.seed + 31) * 1.2f);
                    var r = new RectInt(Mathf.RoundToInt(s.x - s.y * 0.5f), yl, Mathf.RoundToInt(s.y), 2);
                    c.stairCells.Add(r);
                    if (cl.gateStairs && !gated)
                    {
                        gated = true;
                        c.stairs.Add((new Vector2(s.x, yl + 2.5f), new Vector2(s.y + 1f, 1f)));
                    }
                }
            }
        }

        static void Finalise(Ctx c)
        {
            var d = c.d;
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    int k = d.Index(x, y);
                    bool blocked = d.Open[k] == 0 || d.Water[k] != 0 && d.Bridge[k] == 0 ||
                                   d.Cliff[k] == CliffCell.Face || d.Cliff[k] == CliffCell.Waterfall;
                    if (x == 0 || y == 0 || x == c.W - 1 || y == c.H - 1)
                        blocked = true;
                    d.Blocked[k] = (byte)(blocked ? 1 : 0);
                }
            // exits open their border cells
            foreach (var e in c.bp.exits)
                for (int y = Mathf.FloorToInt(e.rect.yMin); y < Mathf.CeilToInt(e.rect.yMax); y++)
                    for (int x = Mathf.FloorToInt(e.rect.xMin); x < Mathf.CeilToInt(e.rect.xMax); x++)
                        if (d.In(x, y) && d.Open[d.Index(x, y)] != 0 && d.Water[d.Index(x, y)] == 0)
                            d.Blocked[d.Index(x, y)] = 0;
        }

        /// <summary>Walkable cells the player cannot reach become forest (no isolated pockets for spawns).</summary>
        static void RemovePockets(Ctx c)
        {
            var dist = c.d.DistanceFrom(WorldMapData.Cell(c.bp.entrySpawn));
            int removed = 0;
            for (int i = 0; i < dist.Length; i++)
            {
                if (c.d.Blocked[i] != 0 || dist[i] >= 0)
                    continue;
                c.d.Blocked[i] = 1;
                if (c.d.Water[i] == 0 && c.d.Cliff[i] != CliffCell.Face)
                {
                    c.d.Open[i] = 0;
                    c.d.Surface[i] = SurfaceCell.None;
                }
                removed++;
            }
            if (removed > 0)
                c.d.Log.Add($"pockets: {removed} unreachable walkable cells turned into forest");
        }

        /// <summary>4-connected step distance from every cell where `source` is true (multi-source BFS).</summary>
        static int[] Distance(Ctx c, Func<int, bool> source)
        {
            var dist = new int[c.W * c.H];
            var q = new Queue<int>();
            for (int i = 0; i < dist.Length; i++)
            {
                if (source(i))
                {
                    dist[i] = 0;
                    q.Enqueue(i);
                }
                else
                    dist[i] = int.MaxValue;
            }
            while (q.Count > 0)
            {
                int k = q.Dequeue(), x = k % c.W, y = k / c.W, nd = dist[k] + 1;
                if (x > 0 && dist[k - 1] > nd) { dist[k - 1] = nd; q.Enqueue(k - 1); }
                if (x < c.W - 1 && dist[k + 1] > nd) { dist[k + 1] = nd; q.Enqueue(k + 1); }
                if (y > 0 && dist[k - c.W] > nd) { dist[k - c.W] = nd; q.Enqueue(k - c.W); }
                if (y < c.H - 1 && dist[k + c.W] > nd) { dist[k + c.W] = nd; q.Enqueue(k + c.W); }
            }
            return dist;
        }

        static void PaintGround(Ctx c)
        {
            var d = c.d;
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    int k = d.Index(x, y);
                    if (d.Open[k] == 0)
                    {
                        d.Ground[k] = 1;
                        continue;
                    }
                    // shadow of the canopy creeps 1-2 tiles into clearings
                    float n = WorldNoise.Fbm(x * 0.21f, y * 0.21f, c.seed + 61, 2);
                    if (d.Surface[k] == SurfaceCell.None && d.Water[k] == 0 && c.distForest[k] <= (n > 0.15f ? 2 : 1))
                        d.Ground[k] = 1;
                }
        }

        static void PaintDetail(Ctx c)
        {
            if (c.biome == null)
                return;
            var d = c.d;
            float cover = c.biome.flowerCoverage;
            for (int y = 1; y < c.H - 1; y++)
                for (int x = 1; x < c.W - 1; x++)
                {
                    int k = d.Index(x, y);
                    if (d.Blocked[k] != 0 || d.Surface[k] != SurfaceCell.None || d.Bridge[k] != 0 || d.Ground[k] != 0 || d.Cliff[k] == CliffCell.Face)
                        continue;
                    float patch = WorldNoise.Fbm(x * 0.16f, y * 0.16f, c.seed + 71, 3);
                    float hue = WorldNoise.Value(x * 0.07f, y * 0.07f, c.seed + 73);
                    float r = Hash01(x, y, c.seed + 75);
                    bool nearMushrooms = NearPoi(c, x, y, PoiKind.MushroomGrove, 1.3f);
                    if (patch > 1f - cover * 3.2f && r < 0.62f)
                        d.Detail[k] = (byte)(hue > 0 ? DetailKind.FlowersPink : DetailKind.FlowersViolet);
                    else if (nearMushrooms && r < 0.22f)
                        d.Detail[k] = (byte)DetailKind.Glowcaps;
                    else if (c.distRoad[k] == 1 && r < 0.18f)
                        d.Detail[k] = (byte)DetailKind.Pebbles;
                    else if (r < c.biome.cloverChance)
                        d.Detail[k] = (byte)DetailKind.Clover;
                    else if (patch > 0.25f && r > 0.93f)
                        d.Detail[k] = (byte)DetailKind.Petals;
                }
        }

        static bool NearPoi(Ctx c, float x, float y, PoiKind kind, float scale)
        {
            foreach (var p in c.d.Pois)
                if (p.kind == kind)
                {
                    float dx = (x - p.center.x) / (p.radius.x * scale), dy = (y - p.center.y) / (p.radius.y * scale);
                    if (dx * dx + dy * dy <= 1f)
                        return true;
                }
            return false;
        }

        static float Hash01(int x, int y, int seed) => WorldNoise.Cell01(x, y, seed);

        // ------------------------------------------------------------------ props
        static PropSpec Spec(Ctx c, string sprite) => c.biome != null ? c.biome.Spec(sprite) : null;

        static float Footprint(Ctx c, string sprite)
        {
            var s = Spec(c, sprite);
            return s != null ? Mathf.Max(0.4f, s.WidthTiles * 0.42f) : 0.6f;
        }

        static bool Free(List<(Vector2 p, float r)> list, Vector2 p, float r)
        {
            foreach (var (q, rq) in list)
                if ((q - p).sqrMagnitude < (r + rq) * (r + rq))
                    return false;
            return true;
        }

        static void Add(Ctx c, string sprite, Vector2 pos, string source, bool big, float radius, bool? fade = null)
        {
            var spec = Spec(c, sprite);
            bool blocks = spec != null && spec.Blocks && source != "wall";
            c.d.Props.Add(new PropPlacement
            {
                sprite = sprite, position = pos, source = source,
                fade = fade ?? (spec != null && spec.fade), collider = blocks,
                colliderRect = blocks ? new Rect(pos.x + spec.box.z - spec.box.x * 0.5f, pos.y + spec.box.w - spec.box.y * 0.5f, spec.box.x, spec.box.y) : default,
            });
            (big ? c.bigProps : c.smallProps).Add((pos, radius));
        }

        /// <summary>True when the prop's collider footprint at `pos` would touch a road or bridge cell.</summary>
        static bool BlocksRoad(Ctx c, string sprite, Vector2 pos)
        {
            var spec = Spec(c, sprite);
            if (spec == null || !spec.Blocks)
                return false;
            float x0 = pos.x + spec.box.z - spec.box.x * 0.5f, y0 = pos.y + spec.box.w - spec.box.y * 0.5f;
            for (int y = Mathf.FloorToInt(y0 + 0.05f); y <= Mathf.FloorToInt(y0 + spec.box.y - 0.05f); y++)
                for (int x = Mathf.FloorToInt(x0 + 0.05f); x <= Mathf.FloorToInt(x0 + spec.box.x - 0.05f); x++)
                    if (c.d.In(x, y) && (c.d.Surface[c.d.Index(x, y)] == SurfaceCell.Path || c.d.Bridge[c.d.Index(x, y)] != 0))
                        return true;
            return false;
        }

        static void PlaceStamps(Ctx c)
        {
            foreach (var pl in c.bp.pois)
            {
                if (pl.poi == null)
                    continue;
                foreach (var s in pl.poi.stamps)
                {
                    if (string.IsNullOrEmpty(s.sprite))
                        continue;
                    var pos = pl.position + s.offset;
                    if (s.ground)
                    {
                        c.d.Props.Add(new PropPlacement { sprite = s.sprite, position = pos, source = "stamp:" + pl.key, ground = true });
                        c.stampCount++;
                        continue;
                    }
                    var spec = Spec(c, s.sprite);
                    bool big = spec != null && (spec.category == "tree" || spec.category == "ruin" || spec.category == "camp");
                    Add(c, s.sprite, pos, "stamp:" + pl.key, big, Footprint(c, s.sprite) * 0.8f);
                    c.stampCount++;
                }
            }
        }

        static void PlaceBridges(Ctx c)
        {
            var d = c.d;
            var seen = new bool[c.W * c.H];
            for (int i = 0; i < seen.Length; i++)
            {
                if (d.Bridge[i] == 0 || seen[i])
                    continue;
                int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
                var q = new Queue<int>();
                q.Enqueue(i);
                seen[i] = true;
                while (q.Count > 0)
                {
                    int k = q.Dequeue(), x = k % c.W, y = k / c.W;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    foreach (var n in new[] { k - 1, k + 1, k - c.W, k + c.W })
                        if (n >= 0 && n < seen.Length && !seen[n] && d.Bridge[n] == d.Bridge[k] && Math.Abs(n % c.W - x) <= 1)
                        {
                            seen[n] = true;
                            q.Enqueue(n);
                        }
                }
                var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                int ri = d.Bridge[i] - 1;
                var road = ri >= 0 && ri < c.bp.roads.Length ? c.bp.roads[ri] : null;
                // a river that continues north/south of the span runs north-south, so the road (and bridge deck)
                // runs east-west; water continuing east/west means a north-south road and the vertical bridge
                int ns = 0, ew = 0;
                for (int x = minX; x <= maxX; x++)
                    ns += d.Get(d.Water, x, maxY + 1) + d.Get(d.Water, x, minY - 1);
                for (int y = minY; y <= maxY; y++)
                    ew += d.Get(d.Water, minX - 1, y) + d.Get(d.Water, maxX + 1, y);
                bool vertical = ew > ns;
                var b = c.biome;
                string sprite = vertical ? (b != null ? b.bridgeVerticalSprite : "PROP_mv_bridge_vertical") : (b != null ? b.bridgeSprite : "PROP_mv_bridge");
                var pos = vertical ? new Vector2(rect.center.x, rect.yMin - 0.6f) : new Vector2(rect.center.x, rect.yMin - 0.35f);
                if (road != null && road.brokenBridge && !vertical && c.poiPos.TryGetValue(road.from, out var fromPos))
                {
                    float side = Mathf.Sign(fromPos.x - rect.center.x);
                    var lever = FindLeverSpot(c, new Vector2(rect.center.x + side * (rect.width * 0.5f + 1.8f), rect.yMax + 0.9f));
                    c.smallProps.Add((lever, 1f));
                    d.Objects.Add(new WorldObject
                    {
                        kind = "bridge_stone", sprite = b != null ? b.bridgeBrokenSprite : "PROP_mv_bridge_broken", openSprite = sprite,
                        position = pos, rect = rect, lever = lever,
                        leverSprite = b != null ? b.leverSprite : null, leverOnSprite = b != null ? b.leverOnSprite : null,
                    });
                    d.Gates.Add(rect);
                }
                else
                {
                    if (road != null && road.brokenBridge)
                        d.Log.Add($"road {road.from}->{road.to}: broken bridge needs an east-west span; left intact");
                    d.Objects.Add(new WorldObject { kind = "bridge", sprite = sprite, position = pos, rect = rect });
                }
                c.smallProps.Add((rect.center, Mathf.Max(rect.width, rect.height) * 0.6f));
            }
        }

        static void PlaceLanterns(Ctx c)
        {
            if (string.IsNullOrEmpty(c.biome.roadLantern))
                return;
            float since = c.biome.lanternSpacing * 0.5f;
            int side = 1;
            Vector2 last = Vector2.positiveInfinity;
            for (int i = 1; i < c.mainRoadSamples.Count; i++)
            {
                var p = c.mainRoadSamples[i];
                float step = Vector2.Distance(c.mainRoadSamples[i - 1], p);
                if (step > 3f)
                {
                    since = c.biome.lanternSpacing * 0.5f; // next road started
                    continue;
                }
                since += step;
                if (since < c.biome.lanternSpacing)
                    continue;
                var tan = (p - c.mainRoadSamples[i - 1]).normalized;
                var pos = p + new Vector2(-tan.y, tan.x) * side * 2.6f;
                var cell = WorldMapData.Cell(pos);
                int k = c.d.In(cell.x, cell.y) ? c.d.Index(cell.x, cell.y) : -1;
                if (k < 0 || c.d.Blocked[k] != 0 || c.d.Surface[k] != SurfaceCell.None || c.distRoad[k] < 1 || (last - pos).sqrMagnitude < 36f ||
                    !Free(c.smallProps, pos, 1.2f) || !Free(c.bigProps, pos, 0.8f) || BlocksRoad(c, c.biome.roadLantern, new Vector2(cell.x + 0.5f, cell.y + 0.3f)))
                    continue;
                Add(c, c.biome.roadLantern, new Vector2(cell.x + 0.5f, cell.y + 0.3f), "lantern", false, 1f);
                last = pos;
                since = 0;
                side = -side;
            }
        }

        static void PlaceWallTrees(Ctx c)
        {
            var b = c.biome;
            if (b.wallTrees == null || b.wallTrees.Length == 0)
                return;
            var weights = new List<float>();
            for (int i = 0; i < b.wallTrees.Length; i++)
                weights.Add(i < b.wallTreeWeights.Length ? b.wallTreeWeights[i] : 1f);
            float s = Mathf.Max(1.5f, b.treeSpacing);
            int row = 0;
            for (float gy = 0.4f; gy < c.H + 1.5f; gy += s * 0.82f, row++)
                for (float gx = (row % 2) * s * 0.5f; gx < c.W + 1f; gx += s)
                {
                    var pos = new Vector2(gx + c.rng.Range(-0.45f, 0.45f) * s, gy + c.rng.Range(-0.3f, 0.3f) * s);
                    string sprite = b.wallTrees[Math.Max(0, c.rng.Weighted(weights))];
                    float keep = c.rng.Value();
                    var cell = WorldMapData.Cell(pos);
                    int cx = Mathf.Clamp(cell.x, 0, c.W - 1), cy = Mathf.Clamp(cell.y, 0, c.H - 1);
                    int k = c.d.Index(cx, cy);
                    if (c.d.Open[k] != 0 || c.d.Cliff[k] == CliffCell.Face)
                        continue;
                    int depth = DistToOpen(c, cx, cy);
                    if (depth > 4 && keep > b.deepForestKeep)
                        continue;
                    Add(c, sprite, pos, "wall", true, s * 0.35f, depth <= 4);
                }
        }

        static int DistToOpen(Ctx c, int x, int y)
        {
            if (c.distOpen == null)
                c.distOpen = Distance(c, i => c.d.Open[i] != 0 && c.d.Blocked[i] == 0);
            return c.distOpen[c.d.Index(x, y)];
        }

        static void Scatter(Ctx c, PropRule rule)
        {
            if (rule.sprites == null || rule.sprites.Length == 0)
                return;
            var d = c.d;
            var cells = new List<int>();
            for (int y = 1; y < c.H - 1; y++)
                for (int x = 1; x < c.W - 1; x++)
                    if (Eligible(c, rule, x, y))
                        cells.Add(d.Index(x, y));
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = c.rng.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            float p = rule.density / 100f;
            int placed = 0;
            foreach (int k in cells)
            {
                int x = k % c.W, y = k / c.W;
                float mult = 1f;
                bool near = false;
                if (rule.biasKinds != null && rule.biasKinds.Length > 0)
                {
                    foreach (var poi in d.Pois)
                        if (Array.IndexOf(rule.biasKinds, poi.kind) >= 0 && (poi.center - new Vector2(x, y)).sqrMagnitude <= rule.biasRadius * rule.biasRadius)
                            near = true;
                    if (near)
                        mult = rule.biasBoost;
                    else if (rule.onlyNearBias)
                        continue;
                }
                if (c.rng.Value() >= p * mult)
                    continue;
                string sprite = rule.sprites[c.rng.Range(0, rule.sprites.Length)];
                var pos = new Vector2(x + c.rng.Range(0.2f, 0.8f), y + c.rng.Range(0.15f, 0.6f));
                float r = Mathf.Max(rule.spacing * 0.5f, Footprint(c, sprite) * 0.6f);
                if (!Free(c.smallProps, pos, r) || BlocksRoad(c, sprite, pos))
                    continue;
                if (rule.habitat != PropHabitat.ForestEdge && !Free(c.bigProps, pos, 0.5f))
                    continue;
                Add(c, sprite, pos, rule.id, false, r);
                placed++;
            }
            d.Log.Add($"rule {rule.id}: {placed} placed from {cells.Count} eligible cells");
        }

        static bool Eligible(Ctx c, PropRule rule, int x, int y)
        {
            var d = c.d;
            int k = d.Index(x, y);
            bool walk = d.Blocked[k] == 0;
            switch (rule.habitat)
            {
                case PropHabitat.ForestWall:
                    return d.Open[k] == 0;
                case PropHabitat.ForestEdge:
                    return d.Open[k] == 0 && d.Cliff[k] != CliffCell.Face && DistToOpen(c, x, y) == 1;
                case PropHabitat.Clearing:
                    if (!walk || d.Surface[k] != SurfaceCell.None || d.Bridge[k] != 0 || d.Cliff[k] == CliffCell.Face)
                        return false;
                    if (c.distRoad[k] <= rule.roadClearance || c.distWater[k] <= 1 || InStair(c, x, y, 2))
                        return false;
                    foreach (var poi in d.Pois)
                    {
                        var def = FindPoi(c, poi.key);
                        float core = def != null ? def.clearCore : 3f;
                        if ((poi.center - new Vector2(x + 0.5f, y + 0.5f)).sqrMagnitude < core * core)
                            return false;
                    }
                    return (new Vector2(x, y) - c.bp.entrySpawn).sqrMagnitude > 9f;
                case PropHabitat.Roadside:
                    return walk && d.Surface[k] == SurfaceCell.None && c.distRoad[k] == 2;
                case PropHabitat.Shore:
                    return walk && d.Surface[k] == SurfaceCell.None && d.Bridge[k] == 0 && c.distWater[k] == 1 && c.distRoad[k] > 1;
                case PropHabitat.WaterSurface:
                    return d.Water[k] != 0 && d.Bridge[k] == 0 && c.distWater[k] == 0 && WaterDepth(c, x, y) >= 2;
            }
            return false;
        }

        /// <summary>Nearest walkable, non-road, non-water cell to `want` (lever stands beside the road).</summary>
        static Vector2 FindLeverSpot(Ctx c, Vector2 want)
        {
            var w = WorldMapData.Cell(want);
            for (int r = 0; r <= 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r)
                            continue;
                        int x = w.x + dx, y = w.y + dy;
                        if (!c.d.Walkable(x, y))
                            continue;
                        int k = c.d.Index(x, y);
                        if (c.d.Surface[k] == SurfaceCell.None && c.d.Bridge[k] == 0 && c.d.Water[k] == 0 && c.d.Walkable(x, y + 1))
                            return new Vector2(x + 0.5f, y + 0.4f);
                    }
            return want;
        }

        static bool InStair(Ctx c, int x, int y, int pad)
        {
            foreach (var r in c.stairCells)
                if (x >= r.xMin - pad && x < r.xMax + pad && y >= r.yMin - pad && y < r.yMax + pad)
                    return true;
            return false;
        }

        static int WaterDepth(Ctx c, int x, int y)
        {
            for (int r = 1; r <= 3; r++)
                if (c.d.Get(c.d.Water, x + r, y) == 0 || c.d.Get(c.d.Water, x - r, y) == 0 || c.d.Get(c.d.Water, x, y + r) == 0 || c.d.Get(c.d.Water, x, y - r) == 0)
                    return r - 1;
            return 3;
        }

        static PoiDefinition FindPoi(Ctx c, string key)
        {
            foreach (var p in c.bp.pois)
                if (p.key == key)
                    return p.poi;
            return null;
        }

        // ------------------------------------------------------------------ spawn grid, objects
        static void PaintSpawn(Ctx c)
        {
            var d = c.d;
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    int k = d.Index(x, y);
                    if (d.Blocked[k] != 0)
                    {
                        d.Spawn[k] = SpawnCell.Blocked;
                        continue;
                    }
                    bool safe = d.Bridge[k] != 0 || InStair(c, x, y, 1) || (new Vector2(x + 0.5f, y + 0.5f) - c.bp.entrySpawn).sqrMagnitude < c.bp.entrySafeRadius * c.bp.entrySafeRadius;
                    int z = d.Zone[k];
                    if (z > 0 && z <= c.bp.pois.Length && c.bp.pois[z - 1].poi != null && c.bp.pois[z - 1].poi.safe)
                        safe = true;
                    d.Spawn[k] = safe ? SpawnCell.WalkNoSpawn : SpawnCell.Spawnable;
                }
        }

        static void BuildObjects(Ctx c)
        {
            var d = c.d;
            foreach (var pl in c.bp.pois)
            {
                if (pl.poi == null)
                    continue;
                foreach (var o in pl.poi.objects)
                {
                    var wo = new WorldObject
                    {
                        kind = o.kind, position = pl.position + o.offset, sprite = o.sprite, label = o.label,
                        lines = o.lines ?? Array.Empty<string>(), flag = o.flag,
                    };
                    if (o.kind == "boss_arena")
                    {
                        var r = pl.poi.clearing;
                        wo.rect = new Rect(pl.position.x - r.x * 0.8f, pl.position.y - r.y * 0.55f, r.x * 1.6f, r.y * 1.4f);
                        wo.spawn = pl.position + o.offset;
                        foreach (var (centre, size) in c.stairs)
                            wo.gates.Add(new Rect(centre - size * 0.5f, size));
                        if (string.IsNullOrEmpty(wo.sprite) && c.bp.biome != null)
                            wo.sprite = c.bp.biome.vineSprite;           // the stair gate closes with the biome's vines
                    }
                    d.Objects.Add(wo);
                }
            }
            foreach (var (ri, p, tan) in c.vineGates)
            {
                if (Mathf.Abs(tan.y) < Mathf.Abs(tan.x))
                {
                    d.Log.Add($"road {c.bp.roads[ri].from}->{c.bp.roads[ri].to}: vine gate needs a north-south stretch; skipped");
                    continue;
                }
                // span the whole walkable corridor at this row so the shortcut is really closed
                var cell = WorldMapData.Cell(p);
                int x0 = cell.x, x1 = cell.x;
                while (d.Walkable(x0 - 1, cell.y) && cell.x - x0 < 14) x0--;
                while (d.Walkable(x1 + 1, cell.y) && x1 - cell.x < 14) x1++;
                float w = x1 - x0 + 1;
                var rect = new Rect(x0, cell.y, w, 1f);
                var bv = c.bp.biome;
                d.Objects.Add(new WorldObject
                {
                    kind = "vine", position = new Vector2(x0 + w * 0.5f, cell.y + 0.2f), block = new Vector2(w, 0.8f), rect = rect,
                    sprite = bv != null ? bv.vineSprite : null, openSprite = bv != null ? bv.vineBurntSprite : null,
                    frames = bv != null && bv.vineBurnFrames != null ? bv.vineBurnFrames : Array.Empty<string>(),
                });
                d.Gates.Add(rect);
            }
            foreach (var e in c.bp.exits)
                d.Objects.Add(new WorldObject { kind = "exit", rect = e.rect, scene = e.scene, spawnId = e.spawn, label = e.label, position = e.rect.center });
        }
    }
}
