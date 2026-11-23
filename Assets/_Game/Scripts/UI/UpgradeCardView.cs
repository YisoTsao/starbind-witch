using System;
using Starbind.Core;
using Starbind.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>One level-up card: kind ribbon, icon in a gem socket, name, divider, description, owned
    /// stacks and the key cap. Rare / cursed pages get a red frame. Built from code like the rest of GameUI.</summary>
    public static class UpgradeCardView
    {
        public static readonly Vector2 Size = new Vector2(420, 560);

        public static Color KindColor(UpgradeKind k) => k switch
        {
            UpgradeKind.Active => new Color(1f, 0.62f, 0.3f),
            UpgradeKind.Element => new Color(0.5f, 0.85f, 1f),
            UpgradeKind.Auto => new Color(0.5f, 0.95f, 0.65f),
            UpgradeKind.Passive => new Color(0.8f, 0.65f, 1f),
            _ => new Color(1f, 0.35f, 0.45f),
        };

        public static string KindName(UpgradeKind k) => k switch
        {
            UpgradeKind.Active => "主動法術改造",
            UpgradeKind.Element => "元素強化",
            UpgradeKind.Auto => "自動法術",
            UpgradeKind.Passive => "通用被動",
            _ => "稀有・失控書頁",
        };

        public static Button Build(Transform parent, UpgradeDefinition card, int index, Vector2 pos, int owned, Action onClick)
        {
            bool cursed = card.kind == UpgradeKind.Cursed;
            var kc = KindColor(card.kind);
            var img = UIKit.Image(parent, "Card" + index, UIKit.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, Size);
            UIKit.Skin(img, "UI_card_draft", 3f, cursed ? new Color(1f, 0.7f, 0.72f) : Color.white);
            img.raycastTarget = true;
            img.gameObject.AddComponent<CanvasGroup>();
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.1f, 0.85f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.7f, 0.55f);
            colors.colorMultiplier = 1.3f;
            b.colors = colors;
            b.onClick.AddListener(() => onClick());
            var t = img.transform;

            // kind ribbon across the top edge
            var ribbon = UIKit.Image(t, "Ribbon", kc, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(312, 48));
            UIKit.Skin(ribbon, "UI_banner_draft", 3f, kc);
            UIKit.Label(ribbon.transform, "Kind", KindName(card.kind), 24, new Color(0.1f, 0.06f, 0.16f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(300, 40)).GetComponent<Shadow>().enabled = false;

            // icon in a glowing socket
            var glow = UIKit.Image(t, "Glow", new Color(kc.r, kc.g, kc.b, 0.35f), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(170, 170), UIKit.Art("VFX_bloom"));
            glow.preserveAspect = false;
            UIKit.Icon(t, "Socket", "UI_socket_draft", new Vector2(0.5f, 1), new Vector2(0, -126), 3f);
            var icon = UIKit.Image(t, "Icon", Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -126), new Vector2(96, 96), GameDatabase.Instance.Sprite(card.icon));
            icon.preserveAspect = true;

            UIKit.Label(t, "Name", card.displayName, 36, cursed ? new Color(1f, 0.6f, 0.65f) : UIKit.Gold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(390, 48));
            UIKit.Icon(t, "Divider", "UI_divider_draft", new Vector2(0.5f, 1), new Vector2(0, -290), 3f);
            UIKit.Label(t, "Desc", card.description, 24, UIKit.Text, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -316), new Vector2(360, 150));
            string stack = card.maxStacks > 1 ? $"書頁等級 {owned} → {owned + 1}（上限 {card.maxStacks}）" : owned > 0 ? "已擁有" : "新書頁";
            UIKit.Label(t, "Stack", stack, 24, UIKit.Muted, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 104), new Vector2(380, 32));

            var key = UIKit.Image(t, "Key", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 52), new Vector2(72, 54));
            UIKit.Skin(key, "UI_keycap_draft", 3f);
            UIKit.Label(key.transform, "K", (index + 1).ToString(), 36, UIKit.Gold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(60, 48));

            UIPop.Add(img.gameObject, 0.06f * index);
            return b;
        }
    }
}
