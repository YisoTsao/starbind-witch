using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.World.Tiles
{
    /// <summary>Draws one large seamless texture across many cells: sprites are the texture sliced into
    /// columns x rows tiles (reading order, top row first), picked by cell position modulo the size.
    /// Hides the 32 px repetition of a plain ground tile without any runtime randomness.</summary>
    [CreateAssetMenu(menuName = "Starbind/Tiles/Periodic Tile")]
    public sealed class PeriodicTile : TileBase
    {
        public int columns = 4;
        public int rows = 4;
        public Sprite[] sprites;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            if (sprites == null || sprites.Length < columns * rows || columns <= 0 || rows <= 0)
                return;
            tileData.sprite = sprites[Index(position, columns, rows)];
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

        /// <summary>Sprite index for a cell: texture row 0 is the top, cell y grows upward.</summary>
        public static int Index(Vector3Int p, int columns, int rows)
        {
            int cx = ((p.x % columns) + columns) % columns;
            int cy = ((p.y % rows) + rows) % rows;
            return (rows - 1 - cy) * columns + cx;
        }
    }
}
