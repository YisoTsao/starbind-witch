using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.World.Tiles
{
    public enum NeighborMaskMode
    {
        /// <summary>256 sprites indexed by the 8-neighbour mask N=1 NE=2 E=4 SE=8 S=16 SW=32 W=64 NW=128.</summary>
        Full8 = 0,
        /// <summary>4 sprites: index = (east same ? 1 : 0) + (west same ? 2 : 0).</summary>
        EastWest = 1,
        /// <summary>8 sprites: index = (north open ? 1 : 0) + (east open ? 2 : 0) + (west open ? 4 : 0).</summary>
        NorthEastWestOpen = 2,
    }

    /// <summary>Autotile that selects a sprite from its neighbours (ASSET_PIPELINE.md §5).
    /// Edges of the 256 variants are generated from a shared field, so any combination is seamless.</summary>
    [CreateAssetMenu(menuName = "Starbind/Tiles/Neighbor Mask Tile")]
    public sealed class NeighborMaskTile : TileBase
    {
        public NeighborMaskMode mode;
        public Sprite[] sprites;
        [Tooltip("Other tiles that count as 'same' (e.g. the waterfall inside a cliff face).")]
        public TileBase[] alsoMatches;

        static readonly Vector3Int[] Dirs =
        {
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, -1, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, 1, 0),
        };

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            int index = Index(position, tilemap);
            if (sprites == null || index >= sprites.Length)
                return;
            tileData.sprite = sprites[index];
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public int Index(Vector3Int p, ITilemap tilemap)
        {
            switch (mode)
            {
                case NeighborMaskMode.EastWest:
                    return (Same(tilemap, p + Dirs[2]) ? 1 : 0) + (Same(tilemap, p + Dirs[6]) ? 2 : 0);
                case NeighborMaskMode.NorthEastWestOpen:
                    return (Same(tilemap, p + Dirs[0]) ? 0 : 1) + (Same(tilemap, p + Dirs[2]) ? 0 : 2) +
                           (Same(tilemap, p + Dirs[6]) ? 0 : 4);
                default:
                    int mask = 0;
                    for (int i = 0; i < 8; i++)
                        if (Same(tilemap, p + Dirs[i]))
                            mask |= 1 << i;
                    return mask;
            }
        }

        bool Same(ITilemap tilemap, Vector3Int p)
        {
            var t = tilemap.GetTile(p);
            if (t == null)
                return false;
            if (t == this)
                return true;
            if (alsoMatches != null)
                foreach (var m in alsoMatches)
                    if (m == t)
                        return true;
            return false;
        }
    }
}
