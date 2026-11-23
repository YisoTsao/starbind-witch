using System;
using Starbind.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Small helpers to build uGUI from code (no prefabs needed). Reference canvas 1920x1080.
    /// Text uses Fusion Pixel 12px (SIL OFL 1.1, Resources/Fonts); sizes should be multiples of 12.
    /// Falls back to a CJK OS font when the asset is missing.</summary>
    public static class UIKit
    {
        public const string FontResource = "Fonts/FusionPixel12_zh_tw";
        static Font font;
        static bool rebuildHooked;
        static Sprite white;

        // opaque: uGUI Outline draws tinted copies of the whole image, which shows through any transparency
        public static readonly Color Panel = new Color(0.10f, 0.07f, 0.16f, 1f);
        public static readonly Color PanelLight = new Color(0.20f, 0.14f, 0.30f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.36f);
        public static readonly Color Text = new Color(0.96f, 0.93f, 1f);
        public static readonly Color Muted = new Color(0.72f, 0.66f, 0.82f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            font = null;
            white = null;
            if (rebuildHooked)
                UnityEngine.Font.textureRebuilt -= PointFilter;
            rebuildHooked = false;
        }

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>(FontResource);
                    if (font != null && !rebuildHooked)
                    {
                        // the dynamic glyph atlas is recreated as it grows: keep it point-sampled so the
                        // 2x / 3x pixel glyphs stay sharp instead of being smeared by bilinear filtering
                        UnityEngine.Font.textureRebuilt += PointFilter;
                        rebuildHooked = true;
                    }
                    Prewarm(font);
                    PointFilter(font);
                }
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "PingFang TC", "Heiti TC", "Microsoft JhengHei", "Noto Sans CJK TC", "Arial Unicode MS", "Arial" }, 24);
                    if (font == null)
                        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        public const string CharsetResource = "Fonts/FusionPixel12_charset";
        static readonly int[] PrewarmSizes = { 12, 24, 36, 48 };

        /// <summary>Puts every glyph the game uses (charset generated from scripts and data) into the dynamic
        /// atlas up front, so the atlas is not rebuilt (a CPU hitch) the first time a new word appears mid-scene.</summary>
        static void Prewarm(Font f)
        {
            var charset = Resources.Load<TextAsset>(CharsetResource);
            if (f == null || charset == null)
                return;
            foreach (var size in PrewarmSizes)
                f.RequestCharactersInTexture(charset.text, size, FontStyle.Normal);
        }

        /// <summary>Called every frame by GameUI: cheap guard that the glyph atlas stays point-sampled.</summary>
        public static void EnsurePointFilter()
        {
            var tex = font != null && font.material != null ? font.material.mainTexture : null;
            if (tex != null && tex.filterMode != FilterMode.Point)
                tex.filterMode = FilterMode.Point;
        }

        static void PointFilter(Font f)
        {
            if (f != null && f == font && f.material != null && f.material.mainTexture != null)
                f.material.mainTexture.filterMode = FilterMode.Point;
        }

        /// <summary>Nearest multiple of the font's 12 px design size (min 12) so glyph pixels map to whole UI pixels.</summary>
        public static int PixelSize(int size) => Mathf.Max(12, Mathf.RoundToInt(size / 12f) * 12);

        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                    var px = new Color32[16];
                    for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                    tex.SetPixels32(px);
                    tex.Apply();
                    white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                }
                return white;
            }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Sprite sprite = null)
        {
            var rt = Rect(parent, name, anchor, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : White;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null)
                img.preserveAspect = true;
            return img;
        }

        public static Image Fill(Transform parent, string name, Color color, Image.FillMethod method = UnityEngine.UI.Image.FillMethod.Horizontal)
        {
            var rt = Stretch(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = White;
            img.color = color;
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = method == UnityEngine.UI.Image.FillMethod.Radial360 ? (int)UnityEngine.UI.Image.Origin360.Top : 0;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 boxSize)
        {
            var rt = Rect(parent, name, anchor, anchor, pivot, pos, boxSize);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = PixelSize(size);
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.8f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button Button(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, int fontSize = 28)
        {
            var img = Image(parent, name, PanelLight, anchor, new Vector2(0.5f, 0.5f), pos, size);
            img.raycastTarget = true;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(3, -3);
            Skin(img, "UI_slot_draft", 3f, new Color(0.86f, 0.8f, 1f));
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            Label(img.transform, "Label", text, fontSize, Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16, 8));
            return b;
        }

        /// <summary>Pixel UI art from tools/art/ui_art.py (null when the database is not loaded, e.g. EditMode).</summary>
        public static Sprite Art(string name) => GameDatabase.Instance != null ? GameDatabase.Instance.Sprite(name) : null;

        /// <summary>Applies a pixel-art frame: 9-slice when the sprite has a border, drawn at an integer
        /// <paramref name="scale"/> (UI px per art px at the 1920x1080 reference). Removes the flat-colour
        /// Outline the frame replaces. Keeps the old flat look when the art is missing.</summary>
        public static Image Skin(Image img, string sprite, float scale = 3f, Color? tint = null)
        {
            var s = Art(sprite);
            if (s == null)
                return img;
            img.sprite = s;
            img.preserveAspect = false;
            img.type = s.border.sqrMagnitude > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 100f / s.pixelsPerUnit / scale;
            img.color = tint ?? Color.white;
            foreach (var o in img.GetComponents<Outline>())
                UnityEngine.Object.Destroy(o);
            return img;
        }

        /// <summary>Framed panel (gold double border, corner gems).</summary>
        public static Image Panel9(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var img = Image(parent, name, Panel, anchor, pivot, pos, size);
            return Skin(img, "UI_panel_draft");
        }

        /// <summary>Icon sprite drawn at an integer scale (crisp pixels).</summary>
        public static Image Icon(Transform parent, string name, string sprite, Vector2 anchor, Vector2 pos, float scale)
        {
            var s = Art(sprite);
            var size = s != null ? new Vector2(s.rect.width, s.rect.height) * scale : new Vector2(16, 16) * scale;
            var img = Image(parent, name, Color.white, anchor, new Vector2(0.5f, 0.5f), pos, size, s);
            img.preserveAspect = false;
            return img;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
