using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.World.Tiles
{
    /// <summary>Picks one of several seamless variants from a hash of the cell position, so the
    /// same map always looks the same (no runtime randomness).</summary>
    [CreateAssetMenu(menuName = "Starbind/Tiles/Variant Tile")]
    public sealed class VariantTile : TileBase
    {
        public Sprite[] sprites;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            if (sprites == null || sprites.Length == 0)
                return;
            tileData.sprite = sprites[Pick(position, sprites.Length)];
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public static int Pick(Vector3Int p, int count)
        {
            unchecked
            {
                uint h = (uint)p.x * 73856093u ^ (uint)p.y * 19349663u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)(h % (uint)count);
            }
        }
    }
}
