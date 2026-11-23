using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>The three-page grimoire (GDD §5.1): Form (每個槽位的形態與數值), Element (元素狀態與反應),
    /// Modifier (法術修飾與本局書頁). Tabs switch with the mouse or 1/2/3. Display only.</summary>
    public sealed class GrimoirePanel
    {
        public static readonly string[] TabNames = { "形態", "元素", "修飾" };
        public const float PageWidth = 1420f, PageHeight = 640f;

        readonly RectTransform root;
        readonly List<Image> tabs = new List<Image>();
        RectTransform page;
        public int Tab { get; private set; }

        public GrimoirePanel(RectTransform panel, int tab)
        {
            root = panel;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var img = UIKit.Image(panel, "Tab" + i, UIKit.PanelLight, new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2((i - 1) * 300, -158), new Vector2(276, 72));
                img.raycastTarget = true;
                img.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(idx));
                UIKit.Label(img.transform, "Name", $"{i + 1}  {TabNames[i]}", 36, UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(260, 60));
                tabs.Add(img);
            }
            var bg = UIKit.Image(panel, "Page", UIKit.Panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -158), new Vector2(PageWidth, PageHeight));
            UIKit.Skin(bg, "UI_page_draft", 3f);
            page = bg.rectTransform;
            UIKit.Label(panel, "Hint", "1 / 2 / 3 切換頁面　｜　Tab / Esc 關閉", 24, UIKit.Muted, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(1000, 36));
            Show(tab);
        }

        public void Show(int tab)
        {
            Tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == Tab;
                UIKit.Skin(tabs[i], on ? "UI_tab_active_draft" : "UI_tab_draft", 3f);
                tabs[i].rectTransform.sizeDelta = new Vector2(276, on ? 80 : 68);
                tabs[i].GetComponentInChildren<Text>().color = on ? UIKit.Gold : UIKit.Muted;
            }
            UIKit.Clear(page);
            var p = PlayerController.Current;
            if (p == null)
                return;
            if (Tab == 0) FormPage(p);
            else if (Tab == 1) ElementPage(p);
            else ModifierPage(p);
        }

        // ---------------------------------------------------------------- page 1: forms
        static string FormName(SpellForm f) => f switch
        {
            SpellForm.Missile => "飛彈",
            SpellForm.Area => "領域・目標點",
            SpellForm.Orbit => "環繞",
            SpellForm.Pull => "漩渦牽引",
            SpellForm.Homing => "追蹤飛彈",
            _ => "脈衝",
        };

        void FormPage(PlayerController p)
        {
            float y = -40;
            Header("主動法術", 40, y);
            Header($"自動法術（{p.Build.Auto.Slots.Count}/{p.Build.Auto.UnlockedSlots} 槽）", 740, y);
            float ly = y - 60, ry = y - 60;
            for (int i = 0; i < 2; i++)
                SpellCard(p.Build.Active(i), i == 0 ? "Q" : "E", 40, ref ly);
            foreach (var s in p.Build.Auto.Slots)
                SpellCard(s, "自動", 740, ref ry);
            for (int i = p.Build.Auto.Slots.Count; i < p.Build.Auto.UnlockedSlots; i++)
                SpellCard(null, "自動", 740, ref ry);
        }

        void SpellCard(RuntimeSpell s, string key, float x, ref float y)
        {
            var row = UIKit.Image(page, "Slot_" + key, UIKit.Panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(640, 128));
            UIKit.Skin(row, "UI_slot_draft", 3f, new Color(0.9f, 0.85f, 1f));
            var t = row.transform;
            UIKit.Icon(t, "Socket", "UI_socket_draft", new Vector2(0, 0.5f), new Vector2(76, 0), 2.4f);
            if (s == null)
            {
                UIKit.Label(t, "Empty", $"[{key}]　空槽", 36, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(148, 0), new Vector2(460, 48));
                y -= 140;
                return;
            }
            var icon = UIKit.Image(t, "Icon", Color.white, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76, 0), new Vector2(64, 64), GameDatabase.Instance.Sprite(s.Def.icon));
            icon.preserveAspect = true;
            UIKit.Label(t, "Name", $"[{key}] {s.Def.displayName}", 36, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(148, -14), new Vector2(480, 44));
            UIKit.Label(t, "Form", $"形態：{FormName(s.Form)}　元素：{ElementName(s.Element)}", 24, ElementColor(s.Element), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(148, -58), new Vector2(480, 32));
            string stats = $"傷害 {s.Damage:0}　冷卻 {s.Cooldown:0.0}s" + (s.MpCost > 0 ? $"　MP {s.MpCost:0}" : "") + (s.Count > 1 ? $"　x{s.Count}" : "");
            UIKit.Label(t, "Stats", stats, 24, UIKit.Text, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(148, -88), new Vector2(480, 32));
            y -= 140;
        }

        // ---------------------------------------------------------------- page 2: elements
        public static string ElementName(ElementType e) => e switch
        {
            ElementType.Fire => "火",
            ElementType.Ice => "冰",
            ElementType.Lightning => "雷",
            ElementType.Wind => "風",
            _ => "奧術",
        };

        static Color ElementColor(ElementType e) => e == ElementType.None ? UIKit.Muted : VFX.SpellFx.ElementColor(e);

        static readonly (ElementType el, string status)[] Elements =
        {
            (ElementType.Fire, "燃燒：持續灼燒傷害"),
            (ElementType.Ice, "冰緩：敵人移動減速"),
            (ElementType.Lightning, "感電：受到的傷害提高"),
            (ElementType.Wind, "牽引：把敵人捲向中心"),
        };

        static readonly (string a, string b, string name, string desc)[] Reactions =
        {
            ("火", "冰", "蒸汽爆裂", "周圍範圍追加傷害，冒出蒸汽"),
            ("火", "雷", "超載爆燃", "主目標周圍額外範圍爆炸"),
            ("冰", "雷", "碎晶雷暴", "擊碎冰凍，電弧跳向附近敵人"),
            ("火", "風", "火焰龍捲", "3 秒追敵移動的火焰區域"),
        };

        void ElementPage(PlayerController p)
        {
            var used = new Dictionary<ElementType, List<string>>();
            void Use(RuntimeSpell s)
            {
                if (s == null) return;
                if (!used.TryGetValue(s.Element, out var l)) used[s.Element] = l = new List<string>();
                if (!l.Contains(s.Def.displayName)) l.Add(s.Def.displayName);
            }
            Use(p.Build.Active(0));
            Use(p.Build.Active(1));
            foreach (var s in p.Build.Auto.Slots) Use(s);

            Header("元素與狀態", 40, -40);
            for (int i = 0; i < Elements.Length; i++)
            {
                var (el, status) = Elements[i];
                var col = ElementColor(el);
                var box = UIKit.Image(page, "El" + i, UIKit.Panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40 + i * 340, -124), new Vector2(320, 190));
                UIKit.Skin(box, "UI_slot_draft", 3f, used.ContainsKey(el) ? Color.white : new Color(0.6f, 0.55f, 0.7f));
                var ribbon = UIKit.Image(box.transform, "Ribbon", col, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(150, 42));
                UIKit.Skin(ribbon, "UI_banner_draft", 3f, col);
                UIKit.Label(ribbon.transform, "N", ElementName(el), 24, new Color(0.1f, 0.06f, 0.16f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(140, 36)).GetComponent<Shadow>().enabled = false;
                UIKit.Label(box.transform, "Status", status, 24, UIKit.Text, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(290, 64));
                string spells = used.TryGetValue(el, out var l) ? "使用中：" + string.Join("、", l) : "尚未使用";
                UIKit.Label(box.transform, "Used", spells, 24, used.ContainsKey(el) ? col : UIKit.Muted, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(290, 72));
            }

            var world = CombatWorld.Exists ? CombatWorld.Instance : null;
            Header($"元素反應（反應傷害 x{(world != null ? world.ReactionDamageMultiplier : 1f):0.00}）", 40, -330);
            for (int i = 0; i < Reactions.Length; i++)
            {
                var r = Reactions[i];
                float x = 40 + (i % 2) * 680, y = -390 - (i / 2) * 100;
                UIKit.Label(page, "R" + i, $"{r.a} ＋ {r.b}　→　{r.name}", 36, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(640, 44));
                UIKit.Label(page, "RD" + i, r.desc, 24, UIKit.Text, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 24, y - 46), new Vector2(620, 32));
            }
        }

        // ---------------------------------------------------------------- page 3: modifiers
        void ModifierPage(PlayerController p)
        {
            Header("法術修飾", 40, -40);
            float y = -100;
            var spells = new List<(string key, RuntimeSpell s)> { ("Q", p.Build.Active(0)), ("E", p.Build.Active(1)) };
            foreach (var s in p.Build.Auto.Slots)
                spells.Add(("自動", s));
            foreach (var (key, s) in spells)
            {
                if (s == null)
                    continue;
                var mods = new List<string>();
                if (s.Def.modifier == SpellModifier.Split || s.Split > 0) mods.Add($"分裂 {Mathf.Max(s.Split, 1)}");
                if (s.Def.modifier == SpellModifier.Pierce || s.Pierce > 0) mods.Add($"穿透 +{s.Pierce}");
                if (s.Returns) mods.Add("折返");
                if (s.Count > 1) mods.Add($"數量 x{s.Count}");
                UIKit.Label(page, "M" + key + s.Id, $"[{key}] {s.Def.displayName}", 24, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, y), new Vector2(380, 32));
                UIKit.Label(page, "MV" + key + s.Id, mods.Count > 0 ? string.Join("　", mods) : "（無修飾）", 24, mods.Count > 0 ? UIKit.Text : UIKit.Muted, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(420, y), new Vector2(560, 32));
                y -= 40;
            }

            y -= 16;
            Header("本局書頁", 40, y);
            y -= 60;
            var run = RunManager.Current;
            var db = GameDatabase.Instance;
            float x = 40;
            int n = 0;
            if (run != null && run.Upgrades != null)
                foreach (var kv in run.Upgrades.Stacks)
                {
                    var def = db.upgrades.Find(u => u.id == kv.Key);
                    var col = def != null ? UpgradeCardView.KindColor(def.kind) : UIKit.Muted;
                    var chip = UIKit.Image(page, "Page" + n, col, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(330, 48));
                    UIKit.Skin(chip, "UI_banner_draft", 3f, col);
                    UIKit.Label(chip.transform, "N", (def != null ? def.displayName : kv.Key) + (kv.Value > 1 ? $" x{kv.Value}" : ""), 24, new Color(0.1f, 0.06f, 0.16f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(310, 40)).GetComponent<Shadow>().enabled = false;
                    n++;
                    x += 344;
                    if (n % 4 == 0)
                    {
                        x = 40;
                        y -= 60;
                    }
                }
            if (n == 0)
                UIKit.Label(page, "None", "（尚無書頁：在森林升級時選擇）", 24, UIKit.Muted, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, y), new Vector2(800, 32));

            var st = p.Vitals.Stats;
            UIKit.Label(page, "Stats", $"HP {Mathf.RoundToInt(p.Vitals.Health.Max)}　MP 回復 {st.Get(StatId.MpRegen):0.0}/秒　移速 x{st.Get(StatId.MoveSpeed):0.00}　反應傷害 x{st.Get(StatId.ReactionDamage):0.00}",
                24, UIKit.Muted, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 24), new Vector2(1340, 32));
        }

        void Header(string text, float x, float y)
        {
            UIKit.Label(page, "H", text, 36, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(640, 44));
            var d = UIKit.Icon(page, "Div", "UI_divider_draft", new Vector2(0, 1), new Vector2(x + 96, y - 50), 3f);
            d.color = new Color(1, 1, 1, 0.8f);
        }
    }
}
