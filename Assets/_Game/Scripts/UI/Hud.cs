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

        public void Build(Transform root)
        {
            db = GameDatabase.Instance;
            // ---- vitals (top-left)
            var vit = UIKit.Image(root, "Vitals", UIKit.Panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(470, 176));
            levelText = UIKit.Label(vit.transform, "Level", "Lv 1", 26, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -10), new Vector2(200, 34));
            statusText = UIKit.Label(vit.transform, "Status", "", 22, UIKit.Muted, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -12), new Vector2(260, 34));
            (hp, hpText) = Bar(vit.transform, "HP", new Vector2(18, -48), new Color(0.88f, 0.25f, 0.32f), 30);
            hpLag = hp.transform.parent.Find("Lag")?.GetComponent<Image>();
            (mp, mpText) = Bar(vit.transform, "MP", new Vector2(18, -86), new Color(0.3f, 0.5f, 0.95f), 24);
            (res, resText) = Bar(vit.transform, "Resonance", new Vector2(18, -118), new Color(0.85f, 0.6f, 1f), 18);
            var xpBg = UIKit.Image(vit.transform, "XP", new Color(0, 0, 0, 0.6f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -146), new Vector2(434, 10));
            xp = UIKit.Fill(xpBg.transform, "Fill", UIKit.Gold);

            // ---- zone (top-right)
            var zone = UIKit.Image(root, "Zone", UIKit.Panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(430, 120));
            zoneText = UIKit.Label(zone.transform, "Name", "", 28, UIKit.Gold, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -10), new Vector2(400, 36));
            objectiveText = UIKit.Label(zone.transform, "Objective", "", 22, UIKit.Text, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -48), new Vector2(400, 30));
            timerText = UIKit.Label(zone.transform, "Timer", "", 22, UIKit.Muted, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -80), new Vector2(400, 30));

            // ---- boss bar (top-centre)
            bossRoot = UIKit.Rect(root, "Boss", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(760, 64));
            bossName = UIKit.Label(bossRoot, "Name", "月蝕菇王", 28, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(760, 34));
            var bossBg = UIKit.Image(bossRoot, "Bg", new Color(0, 0, 0, 0.7f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(760, 22));
            bossFill = UIKit.Fill(bossBg.transform, "Fill", new Color(0.75f, 0.35f, 0.95f));
            bossRoot.gameObject.SetActive(false);

            // ---- skill bar (bottom)
            var bar = UIKit.Rect(root, "SkillBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 150));
            float x = -380;
            staff = MakeSlot(bar, "Staff", x, "左鍵", "ICON_staff"); x += 108;
            q = MakeSlot(bar, "Q", x, "Q", null); x += 108;
            e = MakeSlot(bar, "E", x, "E", null); x += 108;
            r = MakeSlot(bar, "R", x, "R", "ICON_overdrive"); x += 108;
            dash = MakeSlot(bar, "Dash", x, "Space", "ICON_dash"); x += 128;
            pot1 = MakeSlot(bar, "Pot1", x, "1", "PICK_potion_hp"); x += 108;
            pot2 = MakeSlot(bar, "Pot2", x, "2", "PICK_potion_mp");
            for (int i = 0; i < autos.Length; i++)
            {
                autos[i] = MakeSlot(bar, "Auto" + i, -110 + i * 74, "", null, 62, 106);
                autos[i].key.text = "自動";
                autos[i].key.fontSize = 16;
            }
        }

        (Image, Text) Bar(Transform parent, string name, Vector2 pos, Color color, float height)
        {
            var bg = UIKit.Image(parent, name, new Color(0, 0, 0, 0.6f), new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(434, height));
            if (name == "HP")
            {
                var lag = UIKit.Fill(bg.transform, "Lag", new Color(1f, 0.85f, 0.6f, 0.8f));
                lag.fillAmount = 1;
            }
            var fill = UIKit.Fill(bg.transform, "Fill", color);
            var label = UIKit.Label(bg.transform, "Text", "", Mathf.RoundToInt(height * 0.7f), UIKit.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(434, height));
            return (fill, label);
        }

        Slot MakeSlot(RectTransform bar, string name, float x, string key, string icon, float size = 92, float y = 0)
        {
            var s = new Slot();
            s.frame = UIKit.Image(bar, name, UIKit.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, y), new Vector2(size, size));
            var outline = s.frame.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.7f);
            outline.effectDistance = new Vector2(2, -2);
            s.icon = UIKit.Image(s.frame.transform, "Icon", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(size * 0.7f, size * 0.7f), icon != null ? db.Sprite(icon) : null);
            s.cooldown = UIKit.Fill(s.frame.transform, "Cooldown", new Color(0, 0, 0, 0.65f), Image.FillMethod.Radial360);
            s.cooldown.fillClockwise = false;
            s.cooldown.fillAmount = 0;
            s.key = UIKit.Label(s.frame.transform, "Key", key, 18, UIKit.Gold, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -24), new Vector2(size + 30, 24));
            s.count = UIKit.Label(s.frame.transform, "Count", "", 22, UIKit.Text, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 2), new Vector2(60, 28));
            return s;
        }

        public void Bind(PlayerController p) => player = p;

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
            var sprite = db.Sprite(spell.Def.icon);
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
            if (zm != null && zm.TryGet(player.transform.position, out var zone))
                zoneText.text = zone.displayName;
            else
                zoneText.text = GameSession.Instance.SceneTitle;
            objectiveText.text = GameSession.Instance.Objective();

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
                var brain = boss.Brain as BossBrain;
                bossName.text = $"月蝕菇王　第 {(brain != null ? brain.Phase : 1)} 階段" + (brain != null && brain.Exposed ? "　弱點暴露！" : "");
            }
        }
    }
}
