using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Starbind.World.Studio;
using UnityEditor;
using UnityEngine;

namespace Starbind.Editor.WorldStudio
{
    /// <summary>Writes a WorldMapData as the map JSON the scene builder reads (same contract as
    /// tools/map/*.py, plus biomeAsset, dark/detail layers and per-prop lights). Hand-written JSON because
    /// JsonUtility cannot write nested arrays (boss gates). The JSON is committed: it is the reviewable,
    /// diffable record of one generation (seed + generator version).</summary>
    public static class WorldMapExporter
    {
        /// <summary>Prop lights: at most this many Light2D per region, at least LightSpacing tiles apart.</summary>
        public const int MaxLights = 56;
        public const float LightSpacing = 5f;

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        static string Q(string s)
        {
            if (s == null)
                return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char ch in s)
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    default: sb.Append(ch); break;
                }
            return sb.Append('"').ToString();
        }

        static string Arr(params float[] v) => "[" + string.Join(", ", v.Select(F)) + "]";

        static void Rows(StringBuilder sb, string key, string[] rows, bool last = false)
        {
            sb.Append("  ").Append(Q(key)).Append(": [\n");
            for (int i = 0; i < rows.Length; i++)
                sb.Append("   ").Append(Q(rows[i])).Append(i < rows.Length - 1 ? ",\n" : "\n");
            sb.Append("  ]").Append(last ? "\n" : ",\n");
        }

        public static string ToJson(WorldMapData d, RegionBlueprint bp)
        {
            var biome = bp.biome;
            var sb = new StringBuilder(1 << 20);
            sb.Append("{\n");
            sb.Append(" \"id\": ").Append(Q(bp.id)).Append(",\n");
            sb.Append(" \"generator\": ").Append(Q($"WitchWorldStudio v{WorldGenerator.Version}")).Append(",\n");
            sb.Append(" \"width\": ").Append(d.Width).Append(", \"height\": ").Append(d.Height).Append(", \"seed\": ").Append(d.Seed).Append(",\n");
            sb.Append(" \"biome\": ").Append(Q(biome != null ? biome.id : "")).Append(",\n");
            sb.Append(" \"biomeAsset\": ").Append(Q(biome != null ? AssetDatabase.GetAssetPath(biome) : "")).Append(",\n");

            // ---- layers (row 0 = top)
            sb.Append(" \"layers\": {\n");
            Rows(sb, "water", d.Rows((x, y) => d.Get(d.Water, x, y) != 0 ? '~' : '.'));
            Rows(sb, "path", d.Rows((x, y) => d.Get(d.Surface, x, y) == SurfaceCell.Path ? 'p' : d.Get(d.Surface, x, y) == SurfaceCell.Paver ? 'v' : '.'));
            Rows(sb, "dark", d.Rows((x, y) => d.Get(d.Ground, x, y) != 0 ? 'd' : '.'));
            Rows(sb, "detail", d.Rows((x, y) => d.Get(d.Detail, x, y) == 0 ? '.' : (char)('0' + d.Get(d.Detail, x, y))));
            Rows(sb, "cliff", d.Rows((x, y) =>
            {
                byte c = d.Get(d.Cliff, x, y);
                return c == CliffCell.Face ? 'F' : c == CliffCell.Waterfall ? 'W' : c == CliffCell.Rim ? 'R' : c == CliffCell.Stairs ? 'S' : '.';
            }));
            Rows(sb, "collision", d.Rows((x, y) => d.Get(d.Blocked, x, y) != 0 ? '#' : '.'));
            Rows(sb, "spawn", d.Rows((x, y) =>
            {
                byte s = d.Get(d.Spawn, x, y);
                return s == SpawnCell.Spawnable ? 's' : s == SpawnCell.WalkNoSpawn ? 'w' : '.';
            }), true);
            sb.Append(" },\n");

            // ---- props
            var lit = ChooseLights(d, biome);
            sb.Append(" \"props\": [\n");
            for (int i = 0; i < d.Props.Count; i++)
            {
                var p = d.Props[i];
                var spec = biome != null ? biome.Spec(p.sprite) : null;
                sb.Append("  {\"sprite\": ").Append(Q(p.sprite)).Append(", \"x\": ").Append(F(p.position.x)).Append(", \"y\": ").Append(F(p.position.y))
                  .Append(", \"fade\": ").Append(p.fade ? "true" : "false");
                if (p.ground)
                    sb.Append(", \"layer\": \"GroundDecal\", \"order\": 2");
                // deep forest trees sit on collision tiles already: no per-tree collider
                if (p.collider && p.source != "wall" && spec != null)
                    sb.Append(", \"box\": ").Append(Arr(spec.box.x, spec.box.y, spec.box.z, spec.box.w));
                if (lit.Contains(i) && spec != null)
                    sb.Append(", \"light\": ").Append(Arr(spec.lightColor.r, spec.lightColor.g, spec.lightColor.b, spec.light.x, spec.light.y, spec.light.z, spec.light.w));
                sb.Append('}').Append(i < d.Props.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(" ],\n");

            // ---- objects
            sb.Append(" \"objects\": [\n");
            for (int i = 0; i < d.Objects.Count; i++)
            {
                var o = d.Objects[i];
                sb.Append("  {\"kind\": ").Append(Q(o.kind));
                switch (o.kind)
                {
                    case "boss_arena":
                        sb.Append(", \"trigger\": ").Append(Arr(o.rect.x, o.rect.y, o.rect.width, o.rect.height))
                          .Append(", \"bossSpawn\": ").Append(Arr(o.spawn.x, o.spawn.y))
                          .Append(", \"gates\": [").Append(string.Join(", ", o.gates.Select(g => Arr(g.center.x, g.center.y, g.width, g.height)))).Append(']');
                        if (!string.IsNullOrEmpty(o.sprite))
                            sb.Append(", \"sprite\": ").Append(Q(o.sprite));
                        break;
                    case "bridge_stone":
                        sb.Append(", \"x\": ").Append(F(o.position.x)).Append(", \"y\": ").Append(F(o.position.y))
                          .Append(", \"sprite\": ").Append(Q(o.sprite)).Append(", \"openSprite\": ").Append(Q(o.openSprite))
                          .Append(", \"rect\": ").Append(Arr(o.rect.x, o.rect.y, o.rect.width, o.rect.height))
                          .Append(", \"gap\": ").Append(Arr(o.rect.width, o.rect.height))
                          .Append(", \"lever\": ").Append(Arr(o.lever.x, o.lever.y));
                        if (!string.IsNullOrEmpty(o.leverSprite))
                            sb.Append(", \"leverSprite\": ").Append(Q(o.leverSprite)).Append(", \"leverOnSprite\": ").Append(Q(o.leverOnSprite));
                        break;
                    case "vine":
                        sb.Append(", \"x\": ").Append(F(o.position.x)).Append(", \"y\": ").Append(F(o.position.y))
                          .Append(", \"block\": ").Append(Arr(o.block.x, o.block.y));
                        if (!string.IsNullOrEmpty(o.sprite))
                            sb.Append(", \"sprite\": ").Append(Q(o.sprite)).Append(", \"openSprite\": ").Append(Q(o.openSprite))
                              .Append(", \"frames\": [").Append(string.Join(", ", o.frames.Select(Q))).Append(']');
                        break;
                    case "exit":
                        sb.Append(", \"rect\": ").Append(Arr(o.rect.x, o.rect.y, o.rect.width, o.rect.height))
                          .Append(", \"scene\": ").Append(Q(o.scene)).Append(", \"spawn\": ").Append(Q(o.spawnId)).Append(", \"label\": ").Append(Q(o.label));
                        break;
                    default:
                        sb.Append(", \"x\": ").Append(F(o.position.x)).Append(", \"y\": ").Append(F(o.position.y));
                        if (!string.IsNullOrEmpty(o.sprite))
                            sb.Append(", \"sprite\": ").Append(Q(o.sprite));
                        if (o.kind == "chest")
                            sb.Append(", \"page\": ").Append(o.flag ? "true" : "false");
                        if (!string.IsNullOrEmpty(o.label))
                            sb.Append(", \"name\": ").Append(Q(o.label));
                        if (o.lines != null && o.lines.Length > 0)
                            sb.Append(", \"lines\": [").Append(string.Join(", ", o.lines.Select(Q))).Append(']');
                        break;
                }
                sb.Append('}').Append(i < d.Objects.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(" ],\n");

            // ---- spawn points, zones, scene
            var e = d.EntrySpawn;
            sb.Append(" \"spawns\": [{\"id\": \"spawn_from_village\", \"x\": ").Append(F(e.x)).Append(", \"y\": ").Append(F(e.y))
              .Append("}, {\"id\": \"spawn_default\", \"x\": ").Append(F(e.x)).Append(", \"y\": ").Append(F(e.y)).Append("}],\n");
            sb.Append(" \"zones\": [\n");
            foreach (var p in d.Pois.Where(p => p.kind != PoiKind.Junction))
            {
                var r = p.radius * 1.1f;
                sb.Append("  {\"id\": ").Append(Q(SpawnTier(p.kind))).Append(", \"name\": ").Append(Q(p.zoneName))
                  .Append(", \"rect\": ").Append(Arr(p.center.x - r.x, p.center.y - r.y, r.x * 2, r.y * 2))
                  .Append(", \"noSpawn\": ").Append(p.safe ? "true" : "false").Append("},\n");
            }
            sb.Append("  {\"id\": \"Z02\", \"name\": ").Append(Q(bp.displayName))
              .Append(", \"rect\": ").Append(Arr(0, 0, d.Width, d.Height)).Append(", \"noSpawn\": false}\n");
            sb.Append(" ],\n");
            sb.Append(" \"scene\": {\"title\": ").Append(Q(bp.displayName)).Append(", \"objective\": ").Append(Q(bp.objective))
              .Append(", \"intro\": ").Append(Q(bp.displayName + "　" + EntryName(d))).Append(", \"runObjective\": ").Append(Q(bp.objective))
              .Append(", \"chapter\": ").Append(Q(bp.chapterId)).Append(", \"dim\": ").Append(F(biome != null ? biome.characterDim : 0.08f))
              .Append(", \"safe\": false}\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Zone id = SpawnDirector enemy-mix tier (Z01 entrance ... Z05 boss); the HUD shows the name.</summary>
        public static string SpawnTier(PoiKind kind) => kind switch
        {
            PoiKind.Entrance => "Z01",
            PoiKind.Camp or PoiKind.Gathering or PoiKind.Junction => "Z02",
            PoiKind.Altar or PoiKind.Lake or PoiKind.MushroomGrove => "Z03",
            PoiKind.CrystalGrove or PoiKind.AncientTree or PoiKind.Waterfall => "Z04",
            PoiKind.BossArena => "Z05",
            _ => "Z02",
        };

        static string EntryName(WorldMapData d)
        {
            foreach (var p in d.Pois)
                if (p.kind == PoiKind.Entrance)
                    return p.displayName;
            return "";
        }

        /// <summary>Which glowing props get a Light2D: POI stamps and road lanterns first, then the rest,
        /// greedily keeping lights apart and under the cap (URP 2D light cost).</summary>
        public static HashSet<int> ChooseLights(WorldMapData d, BiomeDefinition biome)
        {
            var chosen = new HashSet<int>();
            if (biome == null)
                return chosen;
            var order = Enumerable.Range(0, d.Props.Count)
                .Where(i => biome.Spec(d.Props[i].sprite)?.hasLight == true)
                .OrderBy(i => d.Props[i].source.StartsWith("stamp:") ? 0 : d.Props[i].source == "lantern" ? 1 : 2)
                .ThenBy(i => i)
                .ToList();
            var pos = new List<Vector2>();
            foreach (int i in order)
            {
                if (chosen.Count >= MaxLights)
                    break;
                var p = d.Props[i].position;
                float min = d.Props[i].source.StartsWith("stamp:") ? 2.2f : LightSpacing;
                if (pos.Any(q => (q - p).sqrMagnitude < min * min))
                    continue;
                chosen.Add(i);
                pos.Add(p);
            }
            return chosen;
        }
    }
}
