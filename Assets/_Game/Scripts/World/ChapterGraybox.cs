#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Starbind.CameraRig;
using Starbind.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.World
{
    [Serializable] public class RouteAnchor { public string name; public int x, y; }
    [Serializable] public class ChapterLayout
    {
        public string id, title, previous, next;
        public string[] rows;
        public RouteAnchor[] anchors;
    }

    [Serializable] public class ChapterAtlas { public ChapterLayout[] chapters; }

    /// <summary>Loads the five route studies only in the optional developer walkthrough scene.</summary>
    public sealed class ChapterGraybox : MonoBehaviour
    {
        public TextAsset atlas;
        public TileBase groundTile;
        public TileBase wallTile;
        public Sprite anchorSprite;
        public GameObject playerPrefab;

        void Awake()
        {
            var layouts = JsonUtility.FromJson<ChapterAtlas>(atlas.text).chapters;
            var zones = new List<ZoneMap.Zone>();
            int offset = 0;
            int height = 0;
            foreach (var layout in layouts)
            {
                CreateRegion(layout, offset, zones);
                offset += layout.rows[0].Length + 16;
                height = Mathf.Max(height, layout.rows.Length);
            }
            gameObject.AddComponent<ZoneMap>().Configure(zones.ToArray());
            var entry = SpawnPoint.Find("forest.0");
            var player = Instantiate(playerPrefab, entry.transform.position, Quaternion.identity);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            var lens = camera.GetComponent<Camera>();
            lens.orthographic = true;
            lens.orthographicSize = 270f / 64f;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color32(26, 35, 42, 255);
            camera.transform.position = entry.transform.position + Vector3.back * 10;
            camera.AddComponent<CameraFollow2D>().Configure(player.transform, new Rect(0, 0, offset, height));
        }

        void CreateRegion(ChapterLayout layout, int offset, List<ZoneMap.Zone> zones)
        {
            var grid = new GameObject(layout.title, typeof(Grid));
            grid.transform.SetParent(transform, false);
            grid.transform.localPosition = Vector3.right * offset;
            var floor = Layer(grid.transform, "Walkable");
            var walls = Layer(grid.transform, "Walls");
            walls.color = new Color32(82, 110, 120, 255);
            walls.gameObject.layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid);
            for (int y = 0; y < layout.rows.Length; y++)
            for (int x = 0; x < layout.rows[y].Length; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (layout.rows[y][x] == '.') floor.SetTile(cell, groundTile);
                else walls.SetTile(cell, wallTile);
            }
            walls.gameObject.AddComponent<TilemapCollider2D>();
            for (int i = 0; i < layout.anchors.Length; i++)
            {
                var point = layout.anchors[i];
                string id = layout.id + "." + i;
                var marker = new GameObject(id + " " + point.name);
                marker.transform.SetParent(grid.transform, false);
                marker.transform.localPosition = new Vector3(point.x + .5f, point.y + .5f);
                marker.AddComponent<SpawnPoint>().Configure(id);
                zones.Add(new ZoneMap.Zone
                {
                    id = id, displayName = layout.title + " · " + point.name,
                    rect = new Rect(offset + point.x - 3, point.y - 3, 7, 7), noSpawn = true
                });
                var sr = marker.AddComponent<SpriteRenderer>();
                sr.sprite = anchorSprite;
                sr.sortingLayerName = StarbindLayers.SortGroundDecal;
                string destination = null;
                if (i == 0 && !string.IsNullOrEmpty(layout.previous)) destination = layout.previous + ".4";
                if (i == 4 && !string.IsNullOrEmpty(layout.next)) destination = layout.next + ".0";
                if (destination == null) continue;
                marker.layer = LayerMask.NameToLayer(StarbindLayers.Interactable);
                var area = marker.AddComponent<CircleCollider2D>();
                area.isTrigger = true;
                area.radius = .6f;
                marker.AddComponent<Interactable>().Configure(id, Vector2.up);
                marker.AddComponent<LocalTeleport>().Configure(destination);
            }
            zones.Add(new ZoneMap.Zone
            {
                id = layout.id, displayName = layout.title,
                rect = new Rect(offset, 0, layout.rows[0].Length, layout.rows.Length), noSpawn = true
            });
        }

        static Tilemap Layer(Transform grid, string name)
        {
            var layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid, false);
            layer.GetComponent<TilemapRenderer>().sortingLayerName = StarbindLayers.SortGround;
            return layer.GetComponent<Tilemap>();
        }
    }
}
#endif
