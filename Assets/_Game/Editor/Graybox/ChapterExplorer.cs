using System;
using System.Linq;
using UnityEditor;
using Starbind.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.Editor
{
    // The preview scene and its assets are owned by this window, never by the open game scene.
    public sealed class ChapterExplorer : EditorWindow
    {
        public const string DataPath = "Assets/_Game/Editor/Graybox/chapters.json";
        public static ChapterLayout[] ReadLayouts() =>
            JsonUtility.FromJson<ChapterAtlas>(AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text).chapters;

        ChapterLayout[] layouts;
        ChapterLayout active;
        PreviewRenderUtility preview;
        Tilemap floor, wall, markers;
        Tile block;
        Texture2D pixel;
        Sprite sprite;
        Vector3Int cursor;
        static readonly Color[] Tints = {
            new Color32(99,205,169,255), new Color32(228,180,100,255),
            new Color32(176,123,215,255), new Color32(112,170,233,255),
            new Color32(240,113,105,255), new Color32(250,219,110,255)
        };

        [MenuItem("Starbind/Chapter Graybox Explorer")]
        public static void Open() => GetWindow<ChapterExplorer>("章節灰盒");

        void OnEnable()
        {
            layouts = ReadLayouts();
            preview = new PreviewRenderUtility();
            var root = new GameObject("Chapter grid", typeof(Grid));
            preview.AddSingleGO(root);
            floor = Layer(root, "Walkable", 0);
            wall = Layer(root, "Walls", 1);
            wall.gameObject.AddComponent<TilemapCollider2D>();
            markers = Layer(root, "Anchors", 2);
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            sprite = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            block = CreateInstance<Tile>();
            block.sprite = sprite;
            block.flags = TileFlags.None;
            block.colliderType = Tile.ColliderType.Grid;
            LoadLayout(layouts[0].id, false);
        }

        static Tilemap Layer(GameObject root, string name, int order)
        {
            var layer = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(root.transform, false);
            var r = layer.GetComponent<TilemapRenderer>();
            r.sortingOrder = order;
            // the preview scene has no 2D light: the default lit sprite material can render (nearly) black,
            // so the graybox is drawn unlit and its tile colours show as authored
            var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlit != null)
                r.sharedMaterial = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
            return layer.GetComponent<Tilemap>();
        }

        void OnDisable()
        {
            preview?.Cleanup();
            DestroyImmediate(block);
            DestroyImmediate(sprite);
            DestroyImmediate(pixel);
        }

        public Vector3Int Position => cursor;
        public string CurrentId => active.id;
        public bool IsWalkable(Vector3Int at) => floor.HasTile(at) && !wall.HasTile(at);
        public bool IsWall(Vector3Int at) => wall.GetColliderType(at) == Tile.ColliderType.Grid;

        public void LoadLayout(string id, bool fromNext)
        {
            active = layouts.Single(layout => layout.id == id);
            floor.ClearAllTiles();
            wall.ClearAllTiles();
            markers.ClearAllTiles();
            for (int y = 0; y < active.rows.Length; y++)
                for (int x = 0; x < active.rows[y].Length; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (active.rows[y][x] == '.') Paint(floor, cell, new Color32(94, 118, 115, 255));
                    else Paint(wall, cell, new Color32(41, 50, 63, 255));
                }
            for (int i = 0; i < active.anchors.Length; i++)
                Paint(markers, Cell(i), Tints[i]);
            wall.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            cursor = Cell(fromNext ? 4 : 0);
        }

        Vector3Int Cell(int i) => new Vector3Int(active.anchors[i].x, active.anchors[i].y, 0);
        void Paint(Tilemap layer, Vector3Int at, Color color)
        {
            layer.SetTile(at, block);
            layer.SetColor(at, color);
        }

        public bool Move(Vector3Int delta)
        {
            if (Math.Abs(delta.x) + Math.Abs(delta.y) != 1 || delta.z != 0 || !IsWalkable(cursor + delta)) return false;
            cursor += delta;
            return true;
        }

        public bool Travel()
        {
            if (cursor == Cell(4) && !string.IsNullOrEmpty(active.next))
                LoadLayout(active.next, false);
            else if (cursor == Cell(0) && !string.IsNullOrEmpty(active.previous))
                LoadLayout(active.previous, true);
            else return false;
            return true;
        }

        public Texture RenderMap(Rect rect)
        {
            var cam = preview.camera;
            int width = active.rows[0].Length, height = active.rows.Length;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(height * .5f, width * .5f * rect.height / rect.width) + 1;
            cam.transform.position = new Vector3(width * .5f, height * .5f, -10);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(22, 28, 35, 255);
            var oldTile = markers.GetTile(cursor);
            var oldColor = markers.GetColor(cursor);
            Paint(markers, cursor, Color.white);
            preview.BeginPreview(rect, GUIStyle.none);
            // the preview camera keeps the aspect of whatever it rendered last (in batch runs: not square),
            // which cropped the map and hid most anchors; frame exactly the requested rect
            cam.aspect = rect.width / rect.height;
            preview.Render(true);
            var result = preview.EndPreview();
            markers.SetTile(cursor, oldTile);
            if (oldTile) markers.SetColor(cursor, oldColor);
            return result;
        }

        void OnGUI()
        {
            int current = Array.IndexOf(layouts, active);
            int selected = EditorGUILayout.Popup("章節", current, layouts.Select(layout => layout.title).ToArray());
            if (selected != current) LoadLayout(layouts[selected].id, false);
            GUILayout.Label("方向鍵移動白點，F 在入口／Boss 錨點往返相鄰章節。");
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: Move(Vector3Int.left); break;
                    case KeyCode.RightArrow: Move(Vector3Int.right); break;
                    case KeyCode.UpArrow: Move(Vector3Int.up); break;
                    case KeyCode.DownArrow: Move(Vector3Int.down); break;
                    case KeyCode.F: Travel(); break;
                    default: return;
                }
                e.Use();
                Repaint();
            }
            GUILayout.Label($"{active.title}  ({cursor.x}, {cursor.y})");
            for (int i = 0; i < active.anchors.Length; i++)
            {
                var line = EditorGUILayout.GetControlRect();
                EditorGUI.DrawRect(new Rect(line.x, line.y + 3, 10, 10), Tints[i]);
                GUI.Label(new Rect(line.x + 16, line.y, line.width - 16, line.height), active.anchors[i].name);
            }
            var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (e.type == EventType.Repaint) GUI.DrawTexture(rect, RenderMap(rect));
        }
    }
}
