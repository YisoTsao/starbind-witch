using System;
using System.Collections.Generic;
using Starbind.Core;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>Persistent UI root: HUD, notices, dialogue, level-up cards, results, pause, grimoire,
    /// workshop loadout and research panels. Modal panels pause the game through GamePause and block
    /// gameplay input; keyboard shortcuts (1/2/3, F/Space, Esc) work alongside the mouse.</summary>
    public sealed class GameUI : MonoBehaviour
    {
        public Hud Hud { get; private set; }
        Canvas canvas;
        RectTransform modal;
        Text notice;
        float noticeUntil;
        readonly Queue<string> notices = new Queue<string>();

        // dialogue
        RectTransform dialogueRoot;
        Text dialogueName, dialogueText;
        string[] dialogueLines;
        int dialogueIndex;
        float dialogueOpenedAt;

        string openPanel;
        Action<int> keyChoice;

        public bool BlocksGameplay => openPanel != null || (dialogueRoot != null && dialogueRoot.gameObject.activeSelf);
        public string OpenPanel => openPanel;
        public bool DialogueOpen => dialogueRoot != null && dialogueRoot.gameObject.activeSelf;
        public string LastNotice { get; private set; }
        public List<UpgradeDefinition> OfferedCards { get; private set; }

        public void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var hudRoot = UIKit.Stretch(transform, "HUD");
            Hud = hudRoot.gameObject.AddComponent<Hud>();
            Hud.Build(hudRoot);

            notice = UIKit.Label(transform, "Notice", "", 36, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1400, 60));

            dialogueRoot = UIKit.Image(transform, "Dialogue", UIKit.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1200, 220)).rectTransform;
            dialogueRoot.gameObject.AddComponent<Outline>().effectColor = UIKit.Gold;
            dialogueName = UIKit.Label(dialogueRoot, "Name", "", 30, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -18), new Vector2(600, 40));
            dialogueText = UIKit.Label(dialogueRoot, "Text", "", 28, UIKit.Text, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -66), new Vector2(1140, 120));
            UIKit.Label(dialogueRoot, "Hint", "F / Space / 左鍵 繼續", 20, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 12), new Vector2(400, 30));
            dialogueRoot.gameObject.SetActive(false);

            modal = UIKit.Stretch(transform, "Modal");
            var dim = modal.gameObject.AddComponent<Image>();
            dim.sprite = UIKit.White;
            dim.color = new Color(0.03f, 0.02f, 0.06f, 0.72f);
            modal.gameObject.SetActive(false);

            EnsureEventSystem();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform.parent, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ------------------------------------------------------------------ notices
        public void Notice(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            LastNotice = text;
            if (Time.unscaledTime < noticeUntil - 1.2f && notices.Count < 3)
            {
                notices.Enqueue(text);
                return;
            }
            ShowNotice(text);
        }

        void ShowNotice(string text)
        {
            notice.text = text;
            noticeUntil = Time.unscaledTime + 2.2f;
        }

        // ------------------------------------------------------------------ dialogue
        public void ShowDialogue(string speaker, string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return;
            dialogueLines = lines;
            dialogueIndex = 0;
            dialogueName.text = speaker;
            dialogueText.text = lines[0];
            dialogueOpenedAt = Time.unscaledTime;
            dialogueRoot.gameObject.SetActive(true);
        }

        public void ClearNotices()
        {
            notices.Clear();
            noticeUntil = 0;
            if (notice != null)
                notice.text = "";
        }

        public void CloseDialogue()
        {
            if (dialogueRoot != null)
                dialogueRoot.gameObject.SetActive(false);
        }

        public void AdvanceDialogue()
        {
            if (!DialogueOpen || Time.unscaledTime - dialogueOpenedAt < 0.15f)
                return;
            dialogueOpenedAt = Time.unscaledTime;
            dialogueIndex++;
            if (dialogueIndex >= dialogueLines.Length)
                dialogueRoot.gameObject.SetActive(false);
            else
                dialogueText.text = dialogueLines[dialogueIndex];
        }

        // ------------------------------------------------------------------ modal plumbing
        RectTransform OpenModal(string id, Vector2 size, string title)
        {
            CloseModal();
            openPanel = id;
            GamePause.Push("ui:" + id);
            modal.gameObject.SetActive(true);
            var panel = UIKit.Image(modal, "Panel", UIKit.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size).rectTransform;
            var o = panel.gameObject.AddComponent<Outline>();
            o.effectColor = UIKit.Gold;
            o.effectDistance = new Vector2(4, -4);
            UIKit.Label(panel, "Title", title, 44, UIKit.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(size.x - 40, 60));
            return panel;
        }

        public void CloseModal()
        {
            if (openPanel == null)
                return;
            GamePause.Pop("ui:" + openPanel);
            openPanel = null;
            keyChoice = null;
            UIKit.Clear(modal);
            modal.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ level-up cards
        public void ShowUpgrades(List<UpgradeDefinition> cards, int level, Action<UpgradeDefinition> picked)
        {
            OfferedCards = cards;
            var panel = OpenModal("upgrade", new Vector2(1500, 640), $"等級提升！　Lv {level}　選擇一張書頁");
            var db = GameDatabase.Instance;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                int idx = i;
                float x = (i - (cards.Count - 1) * 0.5f) * 470f;
                var b = UIKit.Button(panel, "Card" + i, "", new Vector2(0.5f, 0.5f), new Vector2(x, -40), new Vector2(430, 460), () => Choose(idx));
                var t = b.transform;
                UIKit.Image(t, "Icon", Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(112, 112), db.Sprite(card.icon));
                UIKit.Label(t, "Name", card.displayName, 34, UIKit.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -164), new Vector2(400, 48));
                UIKit.Label(t, "Kind", KindName(card.kind), 22, card.kind == UpgradeKind.Cursed ? new Color(1f, 0.5f, 0.6f) : UIKit.Muted, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -212), new Vector2(400, 30));
                UIKit.Label(t, "Desc", card.description, 24, UIKit.Text, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -256), new Vector2(380, 160));
                UIKit.Label(t, "Key", $"[{i + 1}]", 26, UIKit.Gold, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(200, 34));
            }
            keyChoice = Choose;

            void Choose(int i)
            {
                if (i < 0 || i >= cards.Count)
                    return;
                var c = cards[i];
                CloseModal();
                Notice("獲得：" + c.displayName);
                picked(c);
            }
        }

        static string KindName(UpgradeKind k) => k switch
        {
            UpgradeKind.Active => "主動法術改造",
            UpgradeKind.Element => "元素強化",
            UpgradeKind.Auto => "自動法術",
            UpgradeKind.Passive => "通用被動",
            _ => "稀有・失控書頁",
        };

        // ------------------------------------------------------------------ results
        public void ShowResults(bool victory, List<string> lines, Action onContinue)
        {
            var panel = OpenModal("results", new Vector2(1200, 640), victory ? "勝利" : "倒下");
            for (int i = 0; i < lines.Count; i++)
                UIKit.Label(panel, "Line" + i, lines[i], i == 0 ? 36 : 28, i == 0 ? UIKit.Gold : UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130 - i * 64), new Vector2(1100, 56));
            UIKit.Button(panel, "Continue", victory ? "返回星露村" : "回到工坊", new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(360, 80), Continue);
            keyChoice = _ => Continue();

            void Continue()
            {
                CloseModal();
                onContinue();
            }
        }

        // ------------------------------------------------------------------ pause
        public void TogglePause()
        {
            if (openPanel == "pause" || openPanel == "grimoire" || openPanel == "loadout" || openPanel == "research")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var panel = OpenModal("pause", new Vector2(700, 600), "暫停");
            UIKit.Button(panel, "Resume", "繼續遊戲", new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(460, 80), CloseModal);
            if (RunManager.Current != null && !RunManager.Current.Finished)
                UIKit.Button(panel, "Abandon", "放棄本局（回工坊）", new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(460, 80), () =>
                {
                    CloseModal();
                    RunManager.Current.Finish(false);
                });
            UIKit.Button(panel, "Grimoire", "魔典 / 目前構築", new Vector2(0.5f, 1), new Vector2(0, -390), new Vector2(460, 80), () => { CloseModal(); ToggleGrimoire(); });
            UIKit.Button(panel, "Quit", "離開遊戲", new Vector2(0.5f, 1), new Vector2(0, -500), new Vector2(460, 80), () =>
            {
                GameSession.Instance.Save();
                Application.Quit();
            });
        }

        // ------------------------------------------------------------------ grimoire (Tab)
        public void ToggleGrimoire()
        {
            if (openPanel == "grimoire")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var p = PlayerController.Current;
            var panel = OpenModal("grimoire", new Vector2(1400, 800), "魔典　目前構築");
            if (p == null)
                return;
            var db = GameDatabase.Instance;
            float y = -120;
            Line(panel, "主動法術", ref y, UIKit.Gold);
            for (int i = 0; i < 2; i++)
                SpellLine(panel, i == 0 ? "Q" : "E", p.Build.Active(i), ref y);
            Line(panel, $"自動法術（{p.Build.Auto.Slots.Count}/{p.Build.Auto.UnlockedSlots} 槽）", ref y, UIKit.Gold);
            foreach (var s in p.Build.Auto.Slots)
                SpellLine(panel, "自動", s, ref y);
            var run = RunManager.Current;
            if (run != null && run.Upgrades != null)
            {
                var parts = new List<string>();
                foreach (var kv in run.Upgrades.Stacks)
                {
                    var def = db.upgrades.Find(u => u.id == kv.Key);
                    parts.Add((def != null ? def.displayName : kv.Key) + (kv.Value > 1 ? " x" + kv.Value : ""));
                }
                Line(panel, "本局書頁：" + (parts.Count > 0 ? string.Join("、", parts) : "（尚無）"), ref y, UIKit.Text);
            }
            var st = p.Vitals.Stats;
            Line(panel, $"HP {Mathf.RoundToInt(p.Vitals.Health.Max)}　MP 回復 {st.Get(Combat.StatId.MpRegen):0.0}/秒　移速 x{st.Get(Combat.StatId.MoveSpeed):0.00}　反應傷害 x{st.Get(Combat.StatId.ReactionDamage):0.00}", ref y, UIKit.Muted);
            UIKit.Label(panel, "Hint", "Tab / Esc 關閉　｜　元素反應：火+冰=蒸汽爆裂　火+雷=超載爆燃　冰+雷=碎晶雷暴　火+風=火焰龍捲", 22, UIKit.Muted, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1360, 40));
        }

        void Line(RectTransform panel, string text, ref float y, Color c)
        {
            UIKit.Label(panel, "L", text, 26, c, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(1280, 40));
            y -= 46;
        }

        void SpellLine(RectTransform panel, string key, RuntimeSpell s, ref float y)
        {
            if (s == null)
            {
                Line(panel, $"[{key}] （空）", ref y, UIKit.Muted);
                return;
            }
            UIKit.Image(panel, "Icon", Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y + 22), new Vector2(44, 44), GameDatabase.Instance.Sprite(s.Def.icon));
            string extra = (s.Pierce > 0 ? $"　穿透+{s.Pierce}" : "") + (s.Split > 0 ? $"　分裂{s.Split}" : "") + (s.Returns ? "　折返" : "") + (s.Count > 1 ? $"　x{s.Count}" : "");
            UIKit.Label(panel, "S", $"[{key}] {s.Def.displayName}　傷害 {s.Damage:0}　冷卻 {s.Cooldown:0.0}s" + (s.MpCost > 0 ? $"　MP {s.MpCost:0}" : "") + extra, 24, UIKit.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, y), new Vector2(1200, 40));
            y -= 50;
        }

        // ------------------------------------------------------------------ workshop: loadout
        public void ShowLoadout()
        {
            var panel = OpenModal("loadout", new Vector2(1500, 1010), "魔典台　法術配置與裝備");
            var session = GameSession.Instance;
            var profile = session.Profile;
            var db = GameDatabase.Instance;
            float y = -120;
            string[] titles = { "Q 主動槽", "E 主動槽", "自動槽（開局）" };
            for (int slot = 0; slot < 3; slot++)
            {
                UIKit.Label(panel, "T" + slot, titles[slot], 28, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(400, 44));
                float x = 340;
                foreach (var def in db.spells)
                {
                    bool auto = def.trigger == SpellTrigger.Auto;
                    if ((slot == 2) != auto || !profile.IsUnlocked(def.id))
                        continue;
                    string current = slot == 0 ? profile.activeQ : slot == 1 ? profile.activeE : (profile.autoSpells.Count > 0 ? profile.autoSpells[0] : "");
                    var d = def;
                    int s = slot;
                    var b = UIKit.Button(panel, def.id, def.displayName, new Vector2(0, 1), new Vector2(x + 110, y), new Vector2(220, 64), () =>
                    {
                        if (s == 0) { if (profile.activeE == d.id) profile.activeE = profile.activeQ; profile.activeQ = d.id; }
                        else if (s == 1) { if (profile.activeQ == d.id) profile.activeQ = profile.activeE; profile.activeE = d.id; }
                        else { profile.autoSpells.Clear(); profile.autoSpells.Add(d.id); }
                        session.ApplyLoadoutToPlayer();
                        session.Save();
                        ShowLoadout();
                    }, 24);
                    if (current == def.id)
                        b.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.65f);
                    x += 240;
                }
                y -= 92;
            }
            foreach (var slot in new[] { Items.EquipSlot.Staff, Items.EquipSlot.Charm })
            {
                UIKit.Label(panel, "T" + slot, slot == Items.EquipSlot.Staff ? "法杖" : "護符", 28, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(400, 44));
                float x = 340;
                foreach (var eq in Items.Equipment.All)
                {
                    if (eq.slot != slot)
                        continue;
                    var e = eq;
                    string current = slot == Items.EquipSlot.Staff ? profile.equippedStaff : profile.equippedCharm;
                    var b = UIKit.Button(panel, e.id, e.name, new Vector2(0, 1), new Vector2(x + 110, y), new Vector2(220, 64), () =>
                    {
                        if (e.slot == Items.EquipSlot.Staff) profile.equippedStaff = e.id; else profile.equippedCharm = e.id;
                        session.ApplyLoadoutToPlayer();
                        session.Save();
                        ShowLoadout();
                    }, 24);
                    if (current == e.id)
                        b.GetComponent<Image>().color = new Color(0.45f, 0.3f, 0.65f);
                    x += 240;
                }
                y -= 90;
            }
            var st = Items.Equipment.Find(profile.equippedStaff);
            var ch = Items.Equipment.Find(profile.equippedCharm);
            UIKit.Label(panel, "EqDesc", $"{st?.name}：{st?.description}　｜　{ch?.name}：{ch?.description}", 21, UIKit.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(1400, 30));
            y -= 44;
            foreach (var def in db.spells)
            {
                if (!profile.IsUnlocked(def.id))
                    continue;
                UIKit.Label(panel, "D" + def.id, $"{def.displayName}：{def.description}", 21, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(1400, 30));
                y -= 32;
            }
            UIKit.Button(panel, "Close", "完成", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 70), CloseModal);
        }

        // ------------------------------------------------------------------ workshop: research
        public void ShowResearch()
        {
            var panel = OpenModal("research", new Vector2(1400, 860), "研究桌　永久研究");
            var s = GameSession.Instance;
            var p = s.Profile;
            UIKit.Label(panel, "Mats", $"星砂 {p.starSand}　孢子 {p.spores}　月露 {p.moonDew}　星核碎片 {p.coreShards}　書頁 {p.pages}　研究點 {p.researchPoints}", 26, UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(1300, 40));
            float y = -170;
            foreach (var node in GameSession.ResearchNodes)
            {
                var n = node;
                int level = s.ResearchLevel(n.id);
                bool maxed = level >= n.maxLevel;
                string cost = maxed ? "已完成" : n.CostText(level);
                UIKit.Label(panel, "N" + n.id, $"{n.name}（{level}/{n.maxLevel}）　{n.description}", 24, maxed ? UIKit.Muted : UIKit.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(920, 44));
                var b = UIKit.Button(panel, "B" + n.id, cost, new Vector2(0, 1), new Vector2(1180, y), new Vector2(300, 56), () =>
                {
                    if (s.TryResearch(n.id))
                    {
                        Notice("研究完成：" + n.name);
                        ShowResearch();
                    }
                    else
                        Notice("材料不足");
                }, 22);
                b.interactable = !maxed;
                y -= 70;
            }
            UIKit.Button(panel, "Close", "完成", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 70), CloseModal);
        }

        // ------------------------------------------------------------------ tick
        void Update()
        {
            if (notice != null)
            {
                float left = noticeUntil - Time.unscaledTime;
                if (left <= 0 && notices.Count > 0)
                {
                    ShowNotice(notices.Dequeue());
                    left = 2.2f;
                }
                notice.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, Mathf.Clamp01(left / 0.5f));
            }
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (DialogueOpen && openPanel == null)
            {
                if ((kb != null && (kb.fKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) ||
                    (mouse != null && mouse.leftButton.wasPressedThisFrame))
                    AdvanceDialogue();
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    dialogueRoot.gameObject.SetActive(false);
            }
            if (openPanel != null && kb != null)
            {
                if (keyChoice != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) keyChoice(0);
                    else if (kb.digit2Key.wasPressedThisFrame) keyChoice(1);
                    else if (kb.digit3Key.wasPressedThisFrame) keyChoice(2);
                }
            }
        }

        /// <summary>Test hook: choose card i of the current level-up offer.</summary>
        public bool ChooseCard(int i)
        {
            if (openPanel != "upgrade" || keyChoice == null)
                return false;
            keyChoice(i);
            return true;
        }

        /// <summary>Test hook: press the single action of the results screen.</summary>
        public bool ContinueResults()
        {
            if (openPanel != "results" || keyChoice == null)
                return false;
            keyChoice(0);
            return true;
        }
    }
}
