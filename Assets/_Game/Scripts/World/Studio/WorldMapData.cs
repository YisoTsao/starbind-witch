using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Starbind.World.Studio
{
    /// <summary>Gameplay object produced by the generator (chest, altar, sign, boss_arena, exit, bridge).
    /// Exported to the scene builder's object list; positions in tiles.</summary>
    [Serializable]
    public sealed class WorldObject
    {
        public string kind;
        public Vector2 position;
        public string sprite;
        public string label;
        public string[] lines = Array.Empty<string>();
        public bool flag;
        /// <summary>exit area, boss trigger or bridge span.</summary>
        public Rect rect;
        public Vector2 spawn;
        public string scene;
        public string spawnId;
        /// <summary>bridge_stone: repaired sprite and lever position; vine: blocker size.</summary>
        public string openSprite;
        public Vector2 lever;
        /// <summary>bridge_stone: lever sprites (off, on); vine: burn animation frames.</summary>
        public string leverSprite, leverOnSprite;
        public string[] frames = Array.Empty<string>();
        public Vector2 block;
        public List<Rect> gates = new List<Rect>();
    }

    /// <summary>Result of one generation: per-cell layers (row-major, y up, x right), props, POIs and
    /// gameplay objects. Plain data, deterministic for (blueprint, seed); never stored in an asset.</summary>
    public sealed class WorldMapData
    {
        public readonly int Width, Height, Seed;
        public readonly string RegionId;

        /// <summary>1 = carved open (clearing, road corridor, shore, water body).</summary>
        public readonly byte[] Open;
        /// <summary>0 = grass, 1 = dark forest floor.</summary>
        public readonly byte[] Ground;
        public readonly byte[] Surface;
        public readonly byte[] Water;
        public readonly byte[] Bridge;
        public readonly byte[] Cliff;
        public readonly byte[] Detail;
        public readonly byte[] Blocked;
        public readonly byte[] Spawn;
        /// <summary>POI index + 1 (0 = none).</summary>
        public readonly byte[] Zone;
        public readonly byte[] Plateau;

        public readonly List<PropPlacement> Props = new List<PropPlacement>();
        public readonly List<PoiInstance> Pois = new List<PoiInstance>();
        public readonly List<WorldObject> Objects = new List<WorldObject>();
        public readonly List<string> Log = new List<string>();
        /// <summary>Optional barriers the player opens (broken bridge gap, vines): every POI must stay
        /// reachable while all of them are still closed.</summary>
        public readonly List<Rect> Gates = new List<Rect>();
        public Vector2 EntrySpawn;

        public WorldMapData(string regionId, int width, int height, int seed)
        {
            RegionId = regionId;
            Width = width;
            Height = height;
            Seed = seed;
            int n = width * height;
            Open = new byte[n];
            Ground = new byte[n];
            Surface = new byte[n];
            Water = new byte[n];
            Bridge = new byte[n];
            Cliff = new byte[n];
            Detail = new byte[n];
            Blocked = new byte[n];
            Spawn = new byte[n];
            Zone = new byte[n];
            Plateau = new byte[n];
        }

        public int Index(int x, int y) => y * Width + x;
        public bool In(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public byte Get(byte[] layer, int x, int y) => In(x, y) ? layer[Index(x, y)] : (byte)0;
        public bool Walkable(int x, int y) => In(x, y) && Blocked[Index(x, y)] == 0;

        public static Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

        /// <summary>4-connected BFS step distance over walkable cells; -1 = unreachable.</summary>
        public int[] DistanceFrom(Vector2Int start)
        {
            var dist = new int[Width * Height];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = -1;
            if (!Walkable(start.x, start.y))
                return dist;
            var q = new Queue<int>();
            int s = Index(start.x, start.y);
            dist[s] = 0;
            q.Enqueue(s);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int cx = c % Width, cy = c / Width;
                Visit(cx + 1, cy); Visit(cx - 1, cy); Visit(cx, cy + 1); Visit(cx, cy - 1);

                void Visit(int x, int y)
                {
                    if (!Walkable(x, y))
                        return;
                    int i = Index(x, y);
                    if (dist[i] >= 0)
                        return;
                    dist[i] = dist[c] + 1;
                    q.Enqueue(i);
                }
            }
            return dist;
        }

        /// <summary>Rows for the map JSON (row 0 = top), one char per cell.</summary>
        public string[] Rows(Func<int, int, char> cell)
        {
            var rows = new string[Height];
            var sb = new StringBuilder(Width);
            for (int r = 0; r < Height; r++)
            {
                sb.Clear();
                int y = Height - 1 - r;
                for (int x = 0; x < Width; x++)
                    sb.Append(cell(x, y));
                rows[r] = sb.ToString();
            }
            return rows;
        }

        public WorldValidation Validate()
        {
            var v = new WorldValidation();
            var dist = DistanceFrom(Cell(EntrySpawn));
            if (!Walkable(Cell(EntrySpawn).x, Cell(EntrySpawn).y))
                v.errors.Add($"entry spawn {EntrySpawn} is not walkable");
            for (int i = 0; i < Blocked.Length; i++)
            {
                if (Blocked[i] != 0)
                    continue;
                v.walkable++;
                if (dist[i] >= 0)
                    v.reachable++;
                if (Spawn[i] == SpawnCell.Spawnable)
                    v.spawnable++;
                if (Spawn[i] == SpawnCell.Spawnable && dist[i] < 0)
                    v.errors.Add($"spawnable cell {i % Width},{i / Width} is unreachable");
            }
            if (v.reachable < v.walkable)
                v.errors.Add($"{v.walkable - v.reachable} walkable cells unreachable from the entry");
            foreach (var p in Pois)
            {
                bool ok = ReachableNear(dist, p.center, Mathf.Max(1.5f, Mathf.Max(p.radius.x, p.radius.y) * 0.75f));
                if (!ok)
                    v.errors.Add($"POI {p.key} ({p.displayName}) unreachable");
                else
                    v.reachablePois++;
            }
            if (Gates.Count > 0)
            {
                var saved = (byte[])Blocked.Clone();
                foreach (var g in Gates)
                    for (int y = Mathf.FloorToInt(g.yMin); y < Mathf.CeilToInt(g.yMax); y++)
                        for (int x = Mathf.FloorToInt(g.xMin); x < Mathf.CeilToInt(g.xMax); x++)
                            if (In(x, y))
                                Blocked[Index(x, y)] = 1;
                var closed = DistanceFrom(Cell(EntrySpawn));
                foreach (var p in Pois)
                    if (!ReachableNear(closed, p.center, Mathf.Max(1.5f, Mathf.Max(p.radius.x, p.radius.y) * 0.75f)))
                        v.errors.Add($"POI {p.key} is only reachable through an optional gate");
                Array.Copy(saved, Blocked, saved.Length);
            }
            foreach (var pr in Props)
            {
                if (!pr.collider || pr.colliderRect.width <= 0)
                    continue;
                var r = pr.colliderRect;
                for (int y = Mathf.FloorToInt(r.yMin + 0.05f); y <= Mathf.FloorToInt(r.yMax - 0.05f); y++)
                    for (int x = Mathf.FloorToInt(r.xMin + 0.05f); x <= Mathf.FloorToInt(r.xMax - 0.05f); x++)
                        if (In(x, y) && (Surface[Index(x, y)] == SurfaceCell.Path || Bridge[Index(x, y)] != 0))
                        {
                            v.errors.Add($"blocking prop {pr.sprite} ({pr.source}) covers road cell {x},{y}");
                            x = int.MaxValue - 1;
                            y = int.MaxValue - 1;
                        }
            }
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if ((x == 0 || y == 0 || x == Width - 1 || y == Height - 1) && Blocked[Index(x, y)] == 0 && !OnExit(x, y))
                        v.errors.Add($"map border cell {x},{y} is walkable");
            v.props = Props.Count;
            v.pois = Pois.Count;
            return v;
        }

        bool OnExit(int x, int y)
        {
            foreach (var o in Objects)
                if (o.kind == "exit" && o.rect.Overlaps(new Rect(x, y, 1, 1)))
                    return true;
            return false;
        }

        bool ReachableNear(int[] dist, Vector2 c, float r)
        {
            int r0 = Mathf.CeilToInt(r);
            var cc = Cell(c);
            for (int y = cc.y - r0; y <= cc.y + r0; y++)
                for (int x = cc.x - r0; x <= cc.x + r0; x++)
                    if (In(x, y) && (x - cc.x) * (x - cc.x) + (y - cc.y) * (y - cc.y) <= r * r && dist[Index(x, y)] >= 0)
                        return true;
            return false;
        }
    }

    public sealed class WorldValidation
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public int walkable, reachable, spawnable, props, pois, reachablePois;
        public bool Ok => errors.Count == 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"walkable {walkable}, reachable {reachable}, spawnable {spawnable}, props {props}, POIs {reachablePois}/{pois} reachable");
            foreach (var e in errors.GetRange(0, Math.Min(errors.Count, 12)))
                sb.Append("\nERROR ").Append(e);
            if (errors.Count > 12)
                sb.Append($"\n... {errors.Count - 12} more errors");
            foreach (var w in warnings)
                sb.Append("\nWARN ").Append(w);
            return sb.ToString();
        }
    }

    /// <summary>Deterministic hash value noise (no global state), for organic shapes.</summary>
    public static class WorldNoise
    {
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000 * 2f - 1f;
            }
        }

        /// <summary>Independent per-cell value in [0, 1).</summary>
        public static float Cell01(int x, int y, int seed) => (Hash(x, y, seed) + 1f) * 0.5f;

        /// <summary>Smooth value noise, about -1..1.</summary>
        public static float Value(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 3)
        {
            float sum = 0, amp = 1, tot = 0, f = 1;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Value(x * f, y * f, seed + i * 7919);
                tot += amp;
                amp *= 0.5f;
                f *= 2f;
            }
            return sum / tot;
        }
    }
}
