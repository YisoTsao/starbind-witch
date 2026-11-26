using Starbind.Core;
using Starbind.Enemies;
using Starbind.Items;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.World;
using UnityEngine;
using UnityEngine.UI;

namespace Starbind.UI
{
    /// <summary>In-game HUD (GDD §12.3): top-left HP/MP/resonance + level/XP, bottom skill bar (staff,
    /// Q, E, R, dash, potions) with cooldowns and auto slots, top-right zone/objective/timer, top-centre
    /// boss bar. Reads player state each frame; never changes it.</summary>
    public sealed class Hud : MonoBehaviour
    {
        sealed class Slot
        {
            public Image frame, icon, cooldown;
            public Text key, count;
        }

        PlayerController player;
        Image hp, mp, res, xp, hpLag;
        Text hpText, mpText, resText, levelText, zoneText, objectiveText, timerText, bossName, statusText;
        RectTransform bossRoot;
        Image bossFill;
        Slot staff, q, e, r, dash, pot1, pot2;
        readonly Slot[] autos = new Slot[AutoCastController.MaxSlots];
        GameDatabase db;
        float hpShown = 1f;

        public MiniMap MiniMap { get; private set; }

        public void Build(Transform root)
        {
            db = GameDatabase.Instance;
            // ---- vitals (top-left): portrait + HP / MP / resonance / XP
            var vit = UIKit.Panel9(root, "Vitals", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(580, 200));
            var portrait = UIKit.Image(vit.transform, "Portrait", UIKit.Panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -22), new Vector2(156, 156));
            UIKit.Skin(portrait, "UI_slot_draft");
            var face = UIKit.Image(portrait.transform, "Face", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(120, 120), Face());
            face.preserveAspect = false;
            levelText = UIKit.Label(vit.transform, "Level", "Lv 1", 26, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(192, -14), new Vector2(200, 34));
            statusText = UIKit.Label(vit.transform, "Status", "", 20, UIKit.Muted, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -16), new Vector2(240, 30));
            (hp, hpText) = Bar(vit.transform, "HP", new Vector2(190, -50), "UI_bar_fill_hp_draft", new Color(0.88f, 0.25f, 0.32f), 38);
            hpLag = hp.transform.parent.Find("Lag")?.GetComponent<Image>();
            (mp, mpText) = Bar(vit.transform, "MP", new Vector2(190, -92), "UI_bar_fill_mp_draft", new Color(0.3f, 0.5f, 0.95f), 32);
            (res, resText) = Bar(vit.transform, "Resonance", new Vector2(190, -128), "UI_bar_fill_res_draft", new Color(0.85f, 0.6f, 1f), 26);
            (xp, _) = Bar(vit.transform, "XP", new Vector2(190, -158), "UI_bar_fill_xp_draft", UIKit.Gold, 20);

            // ---- zone (top-right) and the minimap under it
            var zone = UIKit.Panel9(root, "Zone", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(440, 124));
            // references/02 layout: centred gold title between stars, then the objective line with a diamond bullet
            zoneText = UIKit.Label(zone.transform, "Name", "", 28, UIKit.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(400, 36));
            UIKit.Image(zone.transform, "Rule", new Color(0.83f, 0.66f, 0.32f, 0.55f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(392, 2));
            objectiveText = UIKit.Label(zone.transform, "Objective", "", 21, UIKit.Text, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -56), new Vector2(400, 30));
            timerText = UIKit.Label(zone.transform, "Timer", "", 21, UIKit.Muted, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -86), new Vector2(400, 30));
            var mini = UIKit.Panel9(root, "MiniMap", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -170), new Vector2(330, 252));
            MiniMap = mini.gameObject.AddComponent<MiniMap>();
            MiniMap.Build(mini.rectTransform);
            KeyCap(mini.rectTransform, "M 地圖", new Vector2(0, -16), 120);

            // ---- boss bar (top-centre)
            bossRoot = UIKit.Rect(root, "Boss", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(760, 70));
            bossName = UIKit.Label(bossRoot, "Name", "月蝕菇王", 28, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(760, 34));
            var bossBg = UIKit.Image(bossRoot, "Bg", new Color(0, 0, 0, 0.7f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(760, 30));
            UIKit.Skin(bossBg, "UI_bar_frame_draft", 2f);
            bossFill = InsetFill(bossBg.transform, "Fill", "UI_bar_fill_boss_draft", new Color(0.75f, 0.35f, 0.95f));

            // ---- skill bar (bottom)
            var bar = UIKit.Rect(root, "SkillBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(900, 150));
            skillBar = bar;
            float x = -380;
            staff = MakeSlot(bar, "Staff", x, "左鍵", "ICON_staff"); x += 108;
            q = MakeSlot(bar, "Q", x, "Q", null); x += 108;
            e = MakeSlot(bar, "E", x, "E", null); x += 108;
            r = MakeSlot(bar, "R", x, "R", "ICON_overdrive"); x += 108;
            dash = MakeSlot(bar, "Dash", x, "Space", "ICON_dash"); x += 128;
            pot1 = MakeSlot(bar, "Pot1", x, "1", "ICON_potion_hp"); x += 108;
            pot2 = MakeSlot(bar, "Pot2", x, "2", "ICON_potion_mp");
            for (int i = 0; i < autos.Length; i++)
            {
                autos[i] = MakeSlot(bar, "Auto" + i, -110 + i * 74, "", null, 62, 112);
                autos[i].key.text = "自動";
                autos[i].key.fontSize = UIKit.PixelSize(12);
            }
        }

        /// <summary>The witch's face cut from her idle frame (CHR_witch_ref frame 0, head area).</summary>
        Sprite Face()
        {
            var f = db != null ? db.Sprite("CHR_witch_ref") : null;
            if (f == null)
                return null;
            var rc = f.rect;
            var head = new Rect(rc.x + 13, rc.y + rc.height - 46, 38, 36);
            return Sprite.Create(f.texture, head, new Vector2(0.5f, 0.5f), f.pixelsPerUnit);
        }

        static Image InsetFill(Transform frame, string name, string sprite, Color fallback, float inset = 6)
        {
            var fill = UIKit.Fill(frame, name, fallback);
            var art = UIKit.Art(sprite);
            if (art != null)
            {
                fill.sprite = art;
                fill.color = Color.white;
            }
            fill.rectTransform.offsetMin = new Vector2(inset, inset);
            fill.rectTransform.offsetMax = new Vector2(-inset, -inset);
            return fill;
        }

        static void KeyCap(RectTransform parent, string text, Vector2 pos, float width)
        {
            var cap = UIKit.Image(parent, "KeyCap", UIKit.PanelLight, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 32));
            UIKit.Skin(cap, "UI_keycap_draft", 2f);
            UIKit.Label(cap.transform, "Text", text, 18, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(width, 30));
        }

        (Image, Text) Bar(Transform parent, string name, Vector2 pos, string fillSprite, Color color, float height)
        {
            const float width = 366;
            var bg = UIKit.Image(parent, name, new Color(0, 0, 0, 0.6f), new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(width, height));
            UIKit.Skin(bg, "UI_bar_frame_draft", 2f);
            if (name == "HP")
            {
                var lag = InsetFill(bg.transform, "Lag", "UI_bar_fill_lag_draft", new Color(1f, 0.85f, 0.6f, 0.8f));
                lag.fillAmount = 1;
            }
            var fill = InsetFill(bg.transform, "Fill", fillSprite, color);
            var label = UIKit.Label(bg.transform, "Text", "", Mathf.RoundToInt(height * 0.55f), UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(width, height));
            return (fill, label);
        }

        Slot MakeSlot(RectTransform bar, string name, float x, string key, string icon, float size = 92, float y = 0)
        {
            var s = new Slot();
            s.frame = UIKit.Image(bar, name, UIKit.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, y), new Vector2(size, size));
            var outline = s.frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.7f);
            outline.effectDistance = new Vector2(2, -2);
            UIKit.Skin(s.frame, "UI_slot_draft", size >= 80 ? 3f : 2f);
            float iconSize = size >= 80 ? 64 : 32;   // 32 px icons at 2x / 1x: crisp pixels
            s.icon = UIKit.Image(s.frame.transform, "Icon", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(iconSize, iconSize), icon != null ? UIKit.Art(icon) : null);
            s.icon.preserveAspect = false;
            s.cooldown = UIKit.Fill(s.frame.transform, "Cooldown", new Color(0.02f, 0.0f, 0.06f, 0.7f), Image.FillMethod.Radial360);
            s.cooldown.rectTransform.offsetMin = new Vector2(size * 0.14f, size * 0.14f);
            s.cooldown.rectTransform.offsetMax = new Vector2(-size * 0.14f, -size * 0.14f);
            s.cooldown.fillClockwise = false;
            s.cooldown.fillAmount = 0;
            if (!string.IsNullOrEmpty(key))
            {
                var cap = UIKit.Image(s.frame.transform, "KeyCap", UIKit.PanelLight, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(Mathf.Max(44, key.Length * 16 + 20), 28));
                UIKit.Skin(cap, "UI_keycap_draft", 2f);
            }
            s.key = UIKit.Label(s.frame.transform, "Key", key, 17, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(size + 30, 26));
            s.count = UIKit.Label(s.frame.transform, "Count", "", 22, UIKit.Text, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 8), new Vector2(60, 28));
            return s;
        }

        public void Bind(PlayerController p) => player = p;

        // ------------------------------------------------------------------ touch layout
        RectTransform skillBar;
        readonly System.Collections.Generic.Dictionary<RectTransform, (Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)> desktopLayout =
            new System.Collections.Generic.Dictionary<RectTransform, (Vector2, Vector2, Vector2, Vector2)>();

        public bool TouchLayout { get; private set; }

        /// <summary>Gamepad control each skill slot presses when it is used as a touch button
        /// (the virtual pad from the Input System on-screen controls; StarbindControls binds them).</summary>
        public System.Collections.Generic.IEnumerable<(Image frame, string controlPath)> TouchSlots()
        {
            yield return (staff.frame, "<Gamepad>/rightTrigger");
            yield return (q.frame, "<Gamepad>/buttonWest");
            yield return (e.frame, "<Gamepad>/buttonNorth");
            yield return (r.frame, "<Gamepad>/rightShoulder");
            yield return (dash.frame, "<Gamepad>/leftShoulder");
            yield return (pot1.frame, "<Gamepad>/dpad/left");
            yield return (pot2.frame, "<Gamepad>/dpad/right");
        }

        /// <summary>Touch play: the skill bar becomes a thumb cluster in the bottom-right corner (big
        /// staff button, spells and dash on an arc around it, potions beside), key caps hidden. The
        /// slots keep their icons, cooldowns and counts. false restores the keyboard layout.</summary>
        public void SetTouchLayout(bool touch)
        {
            if (skillBar == null || touch == TouchLayout)
                return;
            TouchLayout = touch;
            var place = new (Slot s, Vector2 c, float size)[]
            {
                (staff, new Vector2(-150, 150), 168), (dash, new Vector2(-350, 80), 108), (q, new Vector2(-340, 248), 108),
                (e, new Vector2(-236, 352), 108), (r, new Vector2(-84, 352), 108), (pot1, new Vector2(-500, 60), 88), (pot2, new Vector2(-500, 166), 88),
            };
            Remember(skillBar);
            foreach (var (s, _, _) in place)
                Remember(s.frame.rectTransform);
            for (int i = 0; i < autos.Length; i++)
                Remember(autos[i].frame.rectTransform);
            if (!touch)
            {
                foreach (var kv in desktopLayout)
                {
                    kv.Key.anchorMin = kv.Key.anchorMax = kv.Value.anchor;
                    kv.Key.pivot = kv.Value.pivot;
                    kv.Key.anchoredPosition = kv.Value.pos;
                    kv.Key.sizeDelta = kv.Value.size;
                }
            }
            else
            {
                skillBar.anchorMin = skillBar.anchorMax = new Vector2(1, 0);
                skillBar.pivot = new Vector2(1, 0);
                skillBar.anchoredPosition = new Vector2(-24, 24);
                skillBar.sizeDelta = new Vector2(600, 520);
                foreach (var (s, c, size) in place)
                    Put(s.frame.rectTransform, c, size);
                for (int i = 0; i < autos.Length; i++)
                    Put(autos[i].frame.rectTransform, new Vector2(-610 + i * 72, 300), 62);
            }
            foreach (var (s, _, _) in place)
            {
                s.frame.raycastTarget = touch;
                s.key.gameObject.SetActive(!touch);
                var cap = s.frame.transform.Find("KeyCap");
                if (cap != null)
                    cap.gameObject.SetActive(!touch);
            }
        }

        void Remember(RectTransform rt)
        {
            if (!desktopLayout.ContainsKey(rt))
                desktopLayout[rt] = (rt.anchorMin, rt.pivot, rt.anchoredPosition, rt.sizeDelta);
        }

        static void Put(RectTransform rt, Vector2 centre, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = centre;
            rt.sizeDelta = new Vector2(size, size);
        }

        void SetSpell(Slot s, RuntimeSpell spell, AbilityRunner runner)
        {
            s.frame.gameObject.SetActive(true);
            if (spell == null)
            {
                s.icon.enabled = false;
                s.cooldown.fillAmount = 0;
                return;
            }
            s.icon.enabled = true;
            var sprite = UIKit.Art(spell.Def.icon);
            if (s.icon.sprite != sprite)
                s.icon.sprite = sprite;
            s.cooldown.fillAmount = runner.CooldownFraction(spell);
            if (spell.Form == SpellForm.Orbit || spell.Form == SpellForm.Homing)
                s.count.text = spell.Count > 1 ? "x" + spell.Count : "";
            else
                s.count.text = spell.MpCost > 0 ? Mathf.RoundToInt(spell.MpCost).ToString() : "";
        }

        void Update()
        {
            if (player == null)
                player = PlayerController.Current;
            if (player == null)
                return;
            var v = player.Vitals;
            var h = v.Health;
            hp.fillAmount = h.Normalized;
            hpShown = Mathf.MoveTowards(hpShown, h.Normalized, Time.unscaledDeltaTime * 0.6f);
            if (hpShown < h.Normalized) hpShown = h.Normalized;
            if (hpLag != null) hpLag.fillAmount = hpShown;
            hpText.text = $"HP {Mathf.CeilToInt(h.Current)} / {Mathf.RoundToInt(h.Max)}";
            mp.fillAmount = v.MpNormalized;
            mpText.text = $"MP {Mathf.FloorToInt(v.Mp)} / {Mathf.RoundToInt(v.MaxMp)}";
            var combat = player.Combat;
            bool od = combat.OverdriveActive;
            float resN = od ? Mathf.Clamp01((combat.Resonance.ActiveUntil - Time.time) / Combat.ResonanceMeter.Duration) : combat.Resonance.Normalized;
            res.fillAmount = resN;
            res.color = od ? new Color(1f, 0.85f, 0.4f) : combat.Resonance.Normalized >= 1f ? Color.Lerp(new Color(0.85f, 0.6f, 1f), Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : new Color(0.85f, 0.6f, 1f);
            resText.text = od ? "超載中" : $"共鳴 {Mathf.FloorToInt(combat.Resonance.Value)}";

            var run = RunManager.Current;
            if (run != null && run.Run != null)
            {
                levelText.text = $"Lv {run.Run.Level}";
                xp.fillAmount = run.Run.XpFraction;
                float t = run.Run.Elapsed;
                timerText.text = $"{(int)(t / 60)}:{(int)(t % 60):00}　擊殺 {run.Run.Kills}　敵 {EnemySpawner.Instance.Alive}";
            }
            else
            {
                levelText.text = "星露村";
                xp.fillAmount = 0;
                timerText.text = "";
            }
            statusText.text = combat.PerfectDodgeReady ? "完美閃避：下一發魔彈強化" : od ? "魔典超載" : "";

            var zm = ZoneMap.Current;
            string zoneName = zm != null && zm.TryGet(player.transform.position, out var zone) ? zone.displayName : GameSession.Instance.SceneTitle;
            zoneText.text = string.IsNullOrEmpty(zoneName) ? "" : "✦ " + zoneName + " ✦";
            string goal = GameSession.Instance.Objective();
            objectiveText.text = string.IsNullOrEmpty(goal) ? "" : "◆ " + goal;

            var runner = player.GetComponent<AbilityRunner>();
            var build = player.Build;
            staff.cooldown.fillAmount = 0;
            staff.count.text = combat.IsCharging ? "蓄力" + Combat.ChargeRules.Level(combat.ChargeHeld) : "";
            SetSpell(q, build.Active(0), runner);
            SetSpell(e, build.Active(1), runner);
            r.cooldown.fillAmount = od ? 0 : 1f - combat.Resonance.Normalized;
            dash.cooldown.fillAmount = player.Motor.DashCooldownFraction;
            pot1.count.text = combat.Bag.Count(Inventory.HpPotion).ToString();
            pot2.count.text = combat.Bag.Count(Inventory.MpPotion).ToString();
            var slots = build.Auto.Slots;
            for (int i = 0; i < autos.Length; i++)
            {
                bool unlocked = i < build.Auto.UnlockedSlots;
                autos[i].frame.gameObject.SetActive(unlocked);
                if (!unlocked) continue;
                if (i < slots.Count)
                    SetSpell(autos[i], slots[i], runner);
                else
                {
                    autos[i].icon.enabled = false;
                    autos[i].cooldown.fillAmount = 0;
                    autos[i].count.text = "";
                }
            }

            var boss = Enemy.ActiveBoss;
            bool showBoss = boss != null && boss.Health != null && !boss.Health.IsDead;
            bossRoot.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                bossFill.fillAmount = boss.Health.Normalized;
                var brain = boss.Brain as BossBrainBase;
                string name = boss.Def != null ? boss.Def.displayName : "Boss";
                string extra = brain is GolemBrain g && g.Charge > 0 ? $"　晶核充能 {g.Charge:0}%" : "";
                bossName.text = $"{name}　第 {(brain != null ? brain.Phase : 1)} 階段" + (brain != null && brain.Exposed ? "　弱點暴露！" : extra);
            }
        }
    }
}
