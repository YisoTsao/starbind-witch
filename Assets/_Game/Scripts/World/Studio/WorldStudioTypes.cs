using System;
using UnityEngine;

namespace Starbind.World.Studio
{
    // Witch World Studio (docs/WORLD_GENERATOR_SPEC.md). Static design data lives in ScriptableObjects
    // (BiomeDefinition, PoiDefinition, RegionBlueprint); WorldGenerator turns them into a WorldMapData
    // (plain C#, never written back into the assets); the Editor exports it as map JSON and builds the scene.

    /// <summary>What a point of interest is; drives default clearing shape, safety and minimap icon.</summary>
    public enum PoiKind
    {
        Entrance = 0,
        Altar = 1,
        AncientTree = 2,
        MushroomGrove = 3,
        Lake = 4,
        Camp = 5,
        Gathering = 6,
        CrystalGrove = 7,
        BossArena = 8,
        Waterfall = 9,
        Junction = 10,
    }

    /// <summary>Ground drawn inside a POI clearing.</summary>
    public enum GroundStyle
    {
        Grass = 0,
        Paver = 1,
        DarkGrass = 2,
    }

    public enum RoadKind
    {
        /// <summary>3 tiles wide, lanterns, always on the critical path.</summary>
        Main = 0,
        /// <summary>2 tiles wide branch to an optional POI.</summary>
        Side = 1,
        /// <summary>1-2 tiles, loops and shortcuts.</summary>
        Trail = 2,
    }

    public enum WaterKind
    {
        Lake = 0,
        River = 1,
    }

    /// <summary>Where a scatter rule may place props.</summary>
    public enum PropHabitat
    {
        /// <summary>Inside the impassable forest mass (big trees).</summary>
        ForestWall = 0,
        /// <summary>Forest cells touching open ground (bushes, ferns, flowers in front of the trees).</summary>
        ForestEdge = 1,
        /// <summary>Open walkable grass away from roads (rocks, flowers, mushrooms, stumps).</summary>
        Clearing = 2,
        /// <summary>Beside main roads (lanterns).</summary>
        Roadside = 3,
        /// <summary>Walkable cells next to water (reeds, rocks).</summary>
        Shore = 4,
        /// <summary>On water at least 2 cells from the shore (lily pads).</summary>
        WaterSurface = 5,
    }

    /// <summary>Detail overlay ids (Detail tilemap); 0 = none. Matches tools/art/moonveil_art.py DETAILS + 1.</summary>
    public enum DetailKind : byte
    {
        None = 0,
        FlowersPink = 1,
        FlowersViolet = 2,
        Clover = 3,
        Pebbles = 4,
        Glowcaps = 5,
        Petals = 6,
    }

    /// <summary>Cliff layer cell values.</summary>
    public static class CliffCell
    {
        public const byte None = 0, Face = 1, Waterfall = 2, Rim = 3, Stairs = 4;
    }

    /// <summary>Surface layer cell values (drawn over ground).</summary>
    public static class SurfaceCell
    {
        public const byte None = 0, Path = 1, Paver = 2;
    }

    /// <summary>Spawn layer cell values (SpawnGrid rows: '.', 'w', 's').</summary>
    public static class SpawnCell
    {
        public const byte Blocked = 0, WalkNoSpawn = 1, Spawnable = 2;
    }

    /// <summary>Static prop description imported from an art manifest (sprite, collider footprint, fade, light).</summary>
    [Serializable]
    public sealed class PropSpec
    {
        public string sprite;
        public string category;
        public Vector2Int pixelSize;
        /// <summary>Collider footprint in tiles: (w, h, offsetX, offsetY); zero size = no collider.</summary>
        public Vector4 box;
        public bool fade;
        public bool hasLight;
        public Color lightColor = Color.white;
        /// <summary>(radius, intensity, offsetX, offsetY)</summary>
        public Vector4 light;

        public float WidthTiles => pixelSize.x / 32f;
        public float HeightTiles => pixelSize.y / 32f;
        public bool Blocks => box.x > 0 && box.y > 0;
    }

    /// <summary>Scatter rule: which sprites may appear in a habitat, how dense and how far apart.</summary>
    [Serializable]
    public sealed class PropRule
    {
        public string id;
        public string[] sprites;
        public PropHabitat habitat;
        /// <summary>Expected props per 100 eligible cells.</summary>
        public float density = 1f;
        /// <summary>Minimum distance (tiles) to any other prop placed by any rule.</summary>
        public float spacing = 1.5f;
        /// <summary>Keep this far (tiles) from road and paver cells.</summary>
        public float roadClearance = 1f;
        /// <summary>Only near these POI kinds (empty = anywhere). Density is multiplied by biasBoost inside biasRadius.</summary>
        public PoiKind[] biasKinds = Array.Empty<PoiKind>();
        public float biasRadius = 12f;
        public float biasBoost = 4f;
        /// <summary>When true the rule only places inside the bias radius of a matching POI.</summary>
        public bool onlyNearBias;
    }

    /// <summary>Fixed prop relative to a POI centre (arch, shrine, tent ...).</summary>
    [Serializable]
    public sealed class PoiStamp
    {
        public string sprite;
        public Vector2 offset;
        /// <summary>Flat ground decal (magic circle, rug) on the GroundDecal layer instead of a World prop.</summary>
        public bool ground;
    }

    /// <summary>Gameplay object relative to a POI centre, exported to the scene builder's object list.
    /// kind: chest, altar, sign, boss_arena, exit (see GameContentBuilder.BuildObject).</summary>
    [Serializable]
    public sealed class PoiObject
    {
        public string kind;
        public Vector2 offset;
        public string sprite;
        public string label;
        [TextArea] public string[] lines = Array.Empty<string>();
        public bool flag;
    }

    [Serializable]
    public sealed class PoiPlacement
    {
        public string key;
        public PoiDefinition poi;
        public Vector2 position;
        public string zoneName;
    }

    [Serializable]
    public sealed class RoadSpec
    {
        public string from;
        public string to;
        public RoadKind kind;
        /// <summary>Intermediate control points (tiles). Endpoints are the POI centres.</summary>
        public Vector2[] via = Array.Empty<Vector2>();
        /// <summary>0 = default for the kind.</summary>
        public float width;
        /// <summary>Meander amplitude (tiles); 0 = default for the kind.</summary>
        public float meander = -1f;
        /// <summary>The bridge on this road starts broken; a lever on the `from` side repairs it (BridgeSwitch).</summary>
        public bool brokenBridge;
        /// <summary>0 = none; otherwise a fire-burnable vine wall across the road corridor at this fraction of
        /// its length (BurnableVine). Only for roads running mostly north-south at that point.</summary>
        [Range(0, 1)] public float vineGateAt;
    }

    [Serializable]
    public sealed class WaterSpec
    {
        public WaterKind kind;
        /// <summary>Lake: one centre point. River: control points (source first).</summary>
        public Vector2[] points = Array.Empty<Vector2>();
        /// <summary>Lake radii (x, y); river: x = width.</summary>
        public Vector2 size = new Vector2(3, 3);
        /// <summary>River end pond radius (0 = none).</summary>
        public float endPond;
    }

    [Serializable]
    public sealed class CliffSpec
    {
        /// <summary>Plateau's southern boundary, x ascending (tiles). Plateau is north of the line.</summary>
        public Vector2[] line = Array.Empty<Vector2>();
        /// <summary>Stair gaps: (centre x, width).</summary>
        public Vector2[] stairs = Array.Empty<Vector2>();
        /// <summary>Gate the first stair with the boss arena (blocks while the boss fight runs).</summary>
        public bool gateStairs;
    }

    [Serializable]
    public sealed class ExitSpec
    {
        public Rect rect;
        public string scene;
        public string spawn;
        public string label;
    }

    /// <summary>One placed prop in the generated map (tile coordinates, pivot at the sprite's ground contact).</summary>
    [Serializable]
    public struct PropPlacement
    {
        public string sprite;
        public Vector2 position;
        /// <summary>Scatter rule id or "stamp:&lt;poi&gt;", "bridge", "edge" ...</summary>
        public string source;
        /// <summary>Overrides the spec: trees deep inside the forest never need to fade.</summary>
        public bool fade;
        public bool collider;
        /// <summary>Collider footprint in tiles (world rect); zero size when the prop does not block.</summary>
        public Rect colliderRect;
        /// <summary>Drawn as a ground decal (GroundDecal sorting layer), never collides.</summary>
        public bool ground;
    }

    /// <summary>One placed POI in the generated map.</summary>
    [Serializable]
    public struct PoiInstance
    {
        public string key;
        public PoiKind kind;
        public string displayName;
        public string zoneName;
        public Vector2 center;
        public Vector2 radius;
        public bool safe;
    }
}
