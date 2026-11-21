using UnityEngine;

namespace Starbind.World
{
    /// <summary>Walkable / spawnable cells of a map as a bit grid (TECH_ARCHITECTURE §6 Markers),
    /// baked by the scene builder from the map JSON. Queried without touching Tilemaps at runtime.
    /// Row 0 of <see cref="rows"/> is the top of the map. '.'=blocked, 'w'=walkable, 's'=spawnable.</summary>
    public sealed class SpawnGrid : MonoBehaviour
    {
        [SerializeField] int width;
        [SerializeField] int height;
        [SerializeField] string[] rows;

        public int Width => width;
        public int Height => height;

        public void Configure(int w, int h, string[] r)
        {
            width = w;
            height = h;
            rows = r;
        }

        char At(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            if (rows == null || x < 0 || y < 0 || x >= width || y >= height)
                return '.';
            var row = rows[height - 1 - y];
            return x < row.Length ? row[x] : '.';
        }

        public bool IsWalkable(Vector2 p) => At(p) != '.';
        public bool IsSpawnable(Vector2 p) => At(p) == 's';

        public int CountSpawnable()
        {
            int n = 0;
            if (rows == null)
                return 0;
            foreach (var r in rows)
                foreach (var c in r)
                    if (c == 's')
                        n++;
            return n;
        }
    }
}
