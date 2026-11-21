using System;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Small helpers to build uGUI from code (no prefabs needed). Reference canvas 1920x1080.
    /// Fonts come from the OS (CJK capable); a licensed pixel font is an asset gap.</summary>
    public static class UIKit
    {
        static Font font;
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
        }

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "PingFang TC", "Heiti TC", "Microsoft JhengHei", "Noto Sans CJK TC", "Arial Unicode MS", "Arial" }, 24);
                    if (font == null)
                        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

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
            t.fontSize = size;
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
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            Label(img.transform, "Label", text, fontSize, Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16, 8));
            return b;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
