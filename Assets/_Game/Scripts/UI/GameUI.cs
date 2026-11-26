using System;
using System.Collections.Generic;
using Starbind.Core;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.VFX;
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
        public TouchControls Touch { get; private set; }
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
            canvas.pixelPerfect = true; // pixel font + pixel frames snap to whole screen pixels
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var hudRoot = UIKit.Stretch(transform, "HUD");
            Hud = hudRoot.gameObject.AddComponent<Hud>();
            Hud.Build(hudRoot);
            Touch = gameObject.AddComponent<TouchControls>();
            Touch.Build(this, Hud, (RectTransform)transform);

            // Keep timed messages below the status panel, clear of the boss and its attack lane.
            notice = UIKit.Label(transform, "Notice", "", 24, UIKit.Gold, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -238), new Vector2(440, 100));

            var dlg = UIKit.Image(transform, "Dialogue", UIKit.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1200, 220));
            dlg.gameObject.AddComponent<Outline>().effectColor = UIKit.Gold;
            UIKit.Skin(dlg, "UI_panel_draft");
            dialogueRoot = dlg.rectTransform;
            dialogueName = UIKit.Label(dialogueRoot, "Name", "", 30, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -18), new Vector2(600, 40));
            dialogueText = UIKit.Label(dialogueRoot, "Text", "", 28, UIKit.Text, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -66), new Vector2(1140, 120));
            UIKit.Label(dialogueRoot, "Hint", "F / Space / 左鍵 / 點擊 繼續", 20, UIKit.Muted, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 12), new Vector2(400, 30));
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
            var panelImg = UIKit.Image(modal, "Panel", UIKit.Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var o = panelImg.gameObject.AddComponent<Outline>();
            o.effectColor = UIKit.Gold;
            o.effectDistance = new Vector2(4, -4);
            UIKit.Skin(panelImg, "UI_panel_draft");
            var panel = panelImg.rectTransform;
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
            var panel = OpenModal("upgrade", new Vector2(1500, 780), $"等級提升！　Lv {level}");
            UIKit.Label(panel, "Sub", "選擇一張書頁寫入魔典", 24, UIKit.Muted, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(800, 36));
            var run = RunManager.Current;
            for (int i = 0; i < cards.Count; i++)
            {
                int idx = i;
                int owned = run != null && run.Upgrades != null && run.Upgrades.Stacks.TryGetValue(cards[i].id, out var n) ? n : 0;
                float x = (i - (cards.Count - 1) * 0.5f) * 460f;
                UpgradeCardView.Build(panel, cards[i], i, new Vector2(x, -40), owned, () => Choose(idx));
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
            if (openPanel == "settings")
            {
                CloseModal();
                TogglePause(); // Esc in settings goes back to the pause menu
                return;
            }
            if (openPanel == "pause" || openPanel == "grimoire" || openPanel == "loadout" || openPanel == "research" || openPanel == "map")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var panel = OpenModal("pause", new Vector2(700, 820), "暫停");
            UIKit.Button(panel, "Resume", "繼續遊戲", new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(460, 80), CloseModal);
            if (RunManager.Current != null && !RunManager.Current.Finished)
                UIKit.Button(panel, "Abandon", "放棄本局（回工坊）", new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(460, 80), () =>
                {
                    CloseModal();
                    RunManager.Current.Finish(false);
                });
            UIKit.Button(panel, "Grimoire", "魔典 / 目前構築", new Vector2(0.5f, 1), new Vector2(0, -390), new Vector2(460, 80), () => { CloseModal(); ToggleGrimoire(); });
            UIKit.Button(panel, "Quests", "任務日誌（J）", new Vector2(0.5f, 1), new Vector2(0, -500), new Vector2(460, 80), () => { CloseModal(); ToggleQuests(); });
            UIKit.Button(panel, "Settings", "設定", new Vector2(0.5f, 1), new Vector2(0, -610), new Vector2(460, 80), ShowSettings);
            UIKit.Button(panel, "Quit", "離開遊戲", new Vector2(0.5f, 1), new Vector2(0, -720), new Vector2(460, 80), () =>
            {
                GameSession.Instance.Save();
                Application.Quit();
            });
        }

        // ------------------------------------------------------------------ settings (from pause)
        /// <summary>Full-screen flash levels offered in settings: strong / soft / off.</summary>
        public static readonly float[] FlashLevels = { 1f, 0.5f, 0f };
        static readonly string[] FlashNames = { "強", "弱", "關" };

        public void ShowSettings()
        {
            var panel = OpenModal("settings", new Vector2(900, 520), "設定");
            UIKit.Label(panel, "FlashTitle", "全螢幕閃光", 36, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -150), new Vector2(260, 48));
            UIKit.Label(panel, "FlashHint", "超載、雷擊與大爆炸時全畫面閃白，對閃光敏感請選「弱」或「關」。", 24, UIKit.Muted, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -260), new Vector2(780, 72));
            var buttons = new Button[FlashLevels.Length];
            for (int i = 0; i < FlashLevels.Length; i++)
            {
                int idx = i;
                buttons[i] = UIKit.Button(panel, "Flash" + i, $"{i + 1}  {FlashNames[i]}", new Vector2(0.5f, 1), new Vector2(-20 + i * 160, -174), new Vector2(140, 64), () => SetFlash(idx));
            }
            UIKit.Button(panel, "Back", "返回", new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(300, 70), () =>
            {
                CloseModal();
                TogglePause();
            });
            keyChoice = SetFlash;
            Refresh();

            void SetFlash(int i)
            {
                if (i < 0 || i >= FlashLevels.Length)
                    return;
                var s = GameSession.Instance;
                s.Profile.screenFlash = FlashLevels[i];
                VfxService.ScreenFlashStrength = FlashLevels[i];
                s.Save();
                Refresh();
            }

            void Refresh()
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool on = Mathf.Approximately(VfxService.ScreenFlashStrength, FlashLevels[i]);
                    buttons[i].GetComponent<Image>().color = on ? new Color(1f, 0.92f, 0.7f) : new Color(0.55f, 0.5f, 0.68f);
                    buttons[i].GetComponentInChildren<Text>().color = on ? UIKit.Gold : UIKit.Muted;
                }
            }
        }

        /// <summary>Test hook: pick flash level i (0 strong, 1 soft, 2 off) in the open settings panel.</summary>
        public bool ChooseSetting(int i)
        {
            if (openPanel != "settings" || keyChoice == null)
                return false;
            keyChoice(i);
            return true;
        }

        // ------------------------------------------------------------------ world map (M)
        public void ToggleMap()
        {
            if (openPanel == "map")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var data = MapData.Current;
            var panel = OpenModal("map", new Vector2(1780, 1010), "地圖・" + (GameSession.Instance.SceneTitle ?? ""));
            if (data == null)
            {
                UIKit.Label(panel, "Empty", "這裡沒有地圖", 30, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 60));
                return;
            }
            WorldMapPanel.Build(panel, data);
        }

        // ------------------------------------------------------------------ grimoire (Tab)
        public GrimoirePanel Grimoire { get; private set; }

        public void ToggleGrimoire()
        {
            if (openPanel == "grimoire")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var panel = OpenModal("grimoire", new Vector2(1500, 900), "魔典");
            Grimoire = new GrimoirePanel(panel, Grimoire?.Tab ?? 0); // reopens on the last page
            keyChoice = i => Grimoire.Show(i);
        }

        // ------------------------------------------------------------------ workshop: loadout
        public void ShowLoadout()
        {
            var title = GameSession.Instance.Profile.HasQuest("configure_first_spell")
                ? "魔典台　法術配置與裝備" : "魔典台　初次書頁：選擇法術放入槽位";
            var panel = OpenModal("loadout", new Vector2(1500, 1010), title);
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
                    if (x > 1250)
                    {
                        x = 340;
                        y -= 72;
                    }
                    string current = slot == 0 ? profile.activeQ : slot == 1 ? profile.activeE : (profile.autoSpells.Count > 0 ? profile.autoSpells[0] : "");
                    var d = def;
                    int s = slot;
                    var b = UIKit.Button(panel, def.id, def.displayName, new Vector2(0, 1), new Vector2(x + 110, y), new Vector2(220, 64), () =>
                    {
                        if (s == 0) { if (profile.activeE == d.id) profile.activeE = profile.activeQ; profile.activeQ = d.id; }
                        else if (s == 1) { if (profile.activeQ == d.id) profile.activeQ = profile.activeE; profile.activeE = d.id; }
                        else { profile.autoSpells.Clear(); profile.autoSpells.Add(d.id); }
                        profile.CompleteQuest("configure_first_spell");
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
                if (!profile.IsUnlocked(def.id) || (def.id != profile.activeQ && def.id != profile.activeE && !profile.autoSpells.Contains(def.id)))
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
                if (!s.ResearchVisible(node))
                    continue;
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
                y -= 64;
            }
            UIKit.Button(panel, "Close", "完成", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 70), CloseModal);
        }

        // ------------------------------------------------------------------ star plate: regions
        public void ShowRegions(World.RegionSelector plate)
        {
            var profile = GameSession.Instance.Profile;
            var chapters = World.RegionSelector.Available(profile);
            var panel = OpenModal("regions", new Vector2(1100, 260 + (chapters.Count + 1) * 96), "傳送星盤");
            float y = -130;
            var actions = new List<Action>();
            void Row(string text, string sub, bool done, Action go)
            {
                int index = actions.Count;
                actions.Add(go);
                var b = UIKit.Button(panel, "R" + index, $"{index + 1}　{text}", new Vector2(0.5f, 1), new Vector2(-140, y), new Vector2(620, 76), go, 28);
                UIKit.Label(panel, "S" + index, sub, 22, done ? UIKit.Gold : UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, y), new Vector2(360, 40));
                y -= 96;
            }
            Row("魔女小屋門口", "村內傳送", false, () => { CloseModal(); plate.TravelHome(); });
            foreach (var c in chapters)
            {
                var ch = c;
                bool cleared = profile.HasQuest(c.victoryFlag);
                Row($"第 {c.order} 章　{c.title}", cleared ? "已通關 ✓" : "未通關", cleared, () => { CloseModal(); World.RegionSelector.Travel(ch); });
            }
            UIKit.Button(panel, "Close", "關閉", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 70), CloseModal);
            keyChoice = i => { if (i >= 0 && i < actions.Count) actions[i](); };
            OfferedRegions = actions.Count;
        }

        /// <summary>Rows in the last region list (tests).</summary>
        public int OfferedRegions { get; private set; }

        /// <summary>Test hook: pick row i of the open choice panel (regions, ending).</summary>
        public bool Choose(int i)
        {
            if (openPanel == null || keyChoice == null)
                return false;
            keyChoice(i);
            return true;
        }

        // ------------------------------------------------------------------ finale: ending choice
        public void ShowEnding(Action onDone)
        {
            var panel = OpenModal("ending", new Vector2(1300, 640), "魔典的抉擇");
            UIKit.Label(panel, "Q", "扭曲的魔典在露米手中顫動。要怎麼做？", 30, UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1200, 50));
            void Pick(bool seal)
            {
                CloseModal();
                var s = GameSession.Instance;
                World.EndingChoice.Choose(s.Profile, seal);
                s.Save();
                ShowDialogue(seal ? "結局 A　封印" : "結局 B　共存", seal ? World.EndingChoice.SealEpilogue : World.EndingChoice.PactEpilogue);
                onDone?.Invoke();
            }
            UIKit.Button(panel, "Seal", "1　封印魔典", new Vector2(0.5f, 0.5f), new Vector2(-300, -10), new Vector2(520, 90), () => Pick(true), 30);
            UIKit.Label(panel, "SealD", "失去改寫法術的力量，讓世界恢復平衡", 22, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -90), new Vector2(560, 40));
            UIKit.Button(panel, "Pact", "2　與魔典締約", new Vector2(0.5f, 0.5f), new Vector2(300, -10), new Vector2(520, 90), () => Pick(false), 30);
            UIKit.Label(panel, "PactD", "成為新的星痕魔女，讓魔典聽命於你", 22, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, -90), new Vector2(560, 40));
            keyChoice = i => { if (i == 0) Pick(true); else if (i == 1) Pick(false); };
        }

        // ------------------------------------------------------------------ quest log (J)
        public void ToggleQuests()
        {
            if (openPanel == "quests")
            {
                CloseModal();
                return;
            }
            if (openPanel != null)
                return;
            var panel = OpenModal("quests", new Vector2(1500, 920), "任務日誌");
            var p = GameSession.Instance.Profile;
            var all = GameDatabase.Instance.quests;
            float y = -110;
            void Section(string title, List<Progression.QuestDefinition> list, Color col, int max)
            {
                UIKit.Label(panel, "H" + title, title, 30, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(600, 44));
                y -= 50;
                if (list.Count == 0)
                {
                    UIKit.Label(panel, "N" + title, "（無）", 22, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, y), new Vector2(600, 34));
                    y -= 40;
                }
                for (int i = 0; i < list.Count && i < max; i++)
                {
                    var q = list[i];
                    string tag = q.kind == Progression.QuestKind.Main ? "主線" : "支線";
                    UIKit.Label(panel, "Q" + q.id, $"［{tag}］{q.title}" + (string.IsNullOrEmpty(q.giver) ? "" : $"　— {q.giver}"), 24, col, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, y), new Vector2(1360, 34));
                    y -= 34;
                    UIKit.Label(panel, "D" + q.id, q.description, 20, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, y), new Vector2(1320, 30));
                    y -= 40;
                }
            }
            Section("進行中", Progression.QuestLog.WithStatus(all, p, Progression.QuestStatus.Active), UIKit.Text, 8);
            var done = Progression.QuestLog.WithStatus(all, p, Progression.QuestStatus.Done);
            done.Reverse();
            Section($"已完成（{done.Count}）", done, UIKit.Muted, 4);
            UIKit.Button(panel, "Close", "關閉（J）", new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 70), CloseModal);
        }

        // ------------------------------------------------------------------ tick
        void Update()
        {
            UIKit.EnsurePointFilter();
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
                    (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                    (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame))
                    AdvanceDialogue();
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    dialogueRoot.gameObject.SetActive(false);
            }
            if (kb != null && kb.jKey.wasPressedThisFrame && !DialogueOpen && (openPanel == null || openPanel == "quests"))
                ToggleQuests();
            if (openPanel != null && kb != null)
            {
                if (keyChoice != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) keyChoice(0);
                    else if (kb.digit2Key.wasPressedThisFrame) keyChoice(1);
                    else if (kb.digit3Key.wasPressedThisFrame) keyChoice(2);
                    else if (kb.digit4Key.wasPressedThisFrame) keyChoice(3);
                    else if (kb.digit5Key.wasPressedThisFrame) keyChoice(4);
                    else if (kb.digit6Key.wasPressedThisFrame) keyChoice(5);
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
