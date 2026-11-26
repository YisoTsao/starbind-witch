using System.Collections;
using System.Linq;
using NUnit.Framework;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.UI;
using Starbind.VFX;
using Starbind.World;
using Starbind.World.Tiles;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Starbind.Tests
{
    /// <summary>2026-11-21 polish pass: pixel font, upgrade card / grimoire layout, minimap objective
    /// marker, and the big spell effects (lights, blasts, sky lightning, screen flash) staying display-only.</summary>
    public class PolishPlayTests : PlayTestBase
    {
        [UnityTest]
        public IEnumerator HudTextUsesThePixelFontAtTwelvePixelSteps()
        {
            yield return Load("Village");
            var texts = GameSession.Instance.UI.GetComponentsInChildren<Text>(true);
            Assert.That(texts.Length, Is.GreaterThan(10));
            Assert.AreEqual(FilterMode.Point, UIKit.Font.material.mainTexture.filterMode, "glyph atlas is point-sampled");
            Assert.IsTrue(GameSession.Instance.UI.GetComponent<Canvas>().pixelPerfect);
            foreach (var t in texts)
            {
                StringAssert.Contains("FusionPixel", t.font.name, t.name);
                Assert.AreEqual(0, t.fontSize % 12, $"{t.name} size {t.fontSize}");
            }
        }

        [UnityTest]
        public IEnumerator UpgradeCardsHaveRibbonSocketAndKeyAndPopIn()
        {
            yield return Load("MoonveilForest");
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            RunManager.Current.Run.AddXp(RunState.XpToNext(1));
            yield return null;
            yield return null;
            var ui = GameSession.Instance.UI;
            Assert.AreEqual("upgrade", ui.OpenPanel);
            var cards = ui.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Card")).ToList();
            Assert.AreEqual(3, cards.Count);
            foreach (var c in cards)
            {
                Assert.IsNotNull(c.transform.Find("Ribbon"), "kind ribbon");
                Assert.IsNotNull(c.transform.Find("Socket"), "icon socket");
                Assert.IsNotNull(c.transform.Find("Key"), "key cap");
                StringAssert.StartsWith("UI_card_draft", c.GetComponent<Image>().sprite.name, "card frame art");
            }
            yield return new WaitForSecondsRealtime(UIPop.Duration + 0.25f);
            foreach (var c in cards)
            {
                Assert.AreEqual(1f, c.GetComponent<CanvasGroup>().alpha, 1e-3, "pop-in finished while paused");
                Assert.AreEqual(1f, c.transform.localScale.x, 1e-3);
            }
            Assert.IsTrue(ui.ChooseCard(1));
        }

        [UnityTest]
        public IEnumerator GrimoireHasThreePagesSwitchedByNumberKeys()
        {
            yield return Load("Village");
            var ui = GameSession.Instance.UI;
            ui.ToggleGrimoire();
            yield return null;
            Assert.AreEqual("grimoire", ui.OpenPanel);
            ui.Grimoire.Show(0); // the panel reopens on whatever page was last used this session
            yield return null;
            Assert.AreEqual(0, ui.Grimoire.Tab);
            var page = ui.GetComponentsInChildren<RectTransform>().First(r => r.name == "Page");
            Assert.IsTrue(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("形態：")), "form page lists spell forms");
            ui.Grimoire.Show(1);
            yield return null;
            Assert.IsTrue(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("蒸汽爆裂")), "element page lists reactions");
            ui.Grimoire.Show(2);
            yield return null;
            Assert.IsTrue(page.GetComponentsInChildren<Text>().Any(t => t.text.Contains("本局書頁")), "modifier page");
            ui.ToggleGrimoire();
            yield return null;
            ui.ToggleGrimoire();
            Assert.AreEqual(2, ui.Grimoire.Tab, "reopens on the last page");
            ui.ToggleGrimoire();
        }

        [UnityTest]
        public IEnumerator MinimapPointsAtTrainingGroundThenAtForestRoad()
        {
            ObjectiveTracker.ResetStatics(); // earlier tests already hit the dummies this session
            yield return Load("Village");
            yield return null;
            Assert.IsTrue(ObjectiveTracker.TryGet(out var goal, out var label));
            Assert.AreEqual("訓練場", label);
            var mini = GameSession.Instance.UI.Hud.MiniMap;
            yield return TeleportTo(goal + new Vector2(-30f, 0f)); // far west: off the minimap
            yield return null;
            Assert.IsTrue(mini.QuestArrowShown, "rim arrow when the objective is off the minimap");
            var arrowDir = mini.QuestArrow.anchoredPosition.normalized;
            Assert.That(Vector2.Angle(arrowDir, (goal - P).normalized), Is.LessThan(8f), "arrow points at the objective");

            yield return TeleportTo(goal + new Vector2(0, -2.5f)); // close: star on the minimap, no arrow
            yield return null;
            Assert.IsFalse(mini.QuestArrowShown);
            Assert.IsTrue(mini.QuestMarker.gameObject.activeSelf);
            Assert.That(Vector2.Distance(mini.QuestMarker.anchoredPosition, (goal - P) * MiniMap.CellPx), Is.LessThan(2f));

            // land enough hits on the dummies: the objective moves on to the south road
            ObjectiveTracker.MarkTrained();
            yield return new WaitForSecondsRealtime(ObjectiveTracker.RefreshInterval + 0.1f);
            Assert.IsTrue(ObjectiveTracker.TryGet(out var road, out label));
            StringAssert.Contains("紫幻森林", label);
            var exit = Object.FindObjectsByType<AreaExit>().First(e => e.TargetScene == "MoonveilForest");
            Assert.That(Vector2.Distance(road, exit.Area.center), Is.LessThan(0.01f));
            StringAssert.Contains("前往紫幻森林", GameSession.Instance.Objective());
        }

        [UnityTest]
        public IEnumerator ForestObjectiveFollowsTheBossArena()
        {
            yield return Load("MoonveilForest");
            RunManager.Current.Director.Running = false;
            yield return null;
            Assert.IsTrue(ObjectiveTracker.TryGet(out var goal, out var label));
            Assert.AreEqual("首領空地", label);
            var arena = Object.FindAnyObjectByType<BossArena>();
            Assert.That(Vector2.Distance(goal, arena.Trigger.center), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator EverySpellPlaysBigEffectsWithoutChangingDamage()
        {
            yield return Load("MoonveilForest");
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            var v = VfxService.Instance;
            var runner = player.GetComponent<AbilityRunner>();
            foreach (var id in new[] { "fire_bolt", "piercing_ice", "split_flame", "chain_spark", "wind_pull" })
            {
                EnemySpawner.Instance.DespawnAll();
                var e = EnemySpawner.Instance.SpawnAt("root_guard", P + new Vector2(0, 3f));
                yield return null;
                player.Build.SetActive(0, id);
                yield return null;
                int casts = SpellFx.CastEffects, impacts = SpellFx.ImpactEffects;
                float hp = e.Health.Current;
                var spell = player.Build.Active(0);
                Assert.IsNotNull(spell, id + " is equippable");
                spell.ReadyAt = 0;
                player.Vitals.RestoreMana(999);
                Assert.AreEqual(CastResult.Cast, runner.TryCast(spell, e.Health.Center), id);
                yield return null;
                Assert.That(SpellFx.CastEffects, Is.GreaterThan(casts), id + " cast effect");
                Assert.That(v.LiveLights, Is.GreaterThan(0), id + " light");
                yield return WaitUntilOrTimeout(() => SpellFx.ImpactEffects > impacts || e.Health.Current < hp, 2.5f);
                yield return new WaitForSeconds(0.1f);
                if (id != "wind_pull")
                    Assert.That(SpellFx.ImpactEffects, Is.GreaterThan(impacts), id + " impact effect");
                Assert.That(e.Health.Current, Is.LessThan(hp), id + " still deals damage");
                yield return new WaitForSeconds(0.6f);
            }
            Assert.That(v.LiveFlipbooks, Is.LessThanOrEqualTo(VfxService.MaxFlipbooks));
            Assert.That(v.AttachedLights, Is.LessThanOrEqualTo(VfxService.MaxAttachedLights));
        }

        [UnityTest]
        public IEnumerator OverdriveFlashesTheScreenAndStrikesLightning()
        {
            yield return Load("MoonveilForest");
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            var v = VfxService.Instance;
            int flashes = v.ScreenFlashes;
            player.Combat.Resonance.ForceFill();
            Assert.IsTrue(player.Combat.TryOverdrive());
            yield return null;
            Assert.That(v.ScreenFlashes, Is.GreaterThan(flashes));
            Assert.That(v.ScreenFlashAlpha, Is.GreaterThan(0.1f).And.LessThanOrEqualTo(0.55f));
            Assert.That(v.LiveArcs, Is.GreaterThan(4), "sky lightning bolts");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, v.ScreenFlashAlpha, 1e-3, "flash fades out");

            VfxService.ScreenFlashStrength = 0f; // accessibility switch
            int before = v.ScreenFlashes;
            v.ScreenFlash(Color.white, 0.5f, 0.2f);
            Assert.AreEqual(before, v.ScreenFlashes);
            VfxService.ScreenFlashStrength = 1f;
        }

        // ------------------------------------------------------------------ round 2: settings, cliff, effect art
        [UnityTest]
        public IEnumerator FlashSettingIsInThePauseMenuAndSaved()
        {
            yield return Load("Village");
            var ui = GameSession.Instance.UI;
            ui.TogglePause();
            yield return null;
            var settingsButton = ui.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "Settings");
            Assert.IsNotNull(settingsButton, "pause menu has a settings button");
            settingsButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual("settings", ui.OpenPanel);
            Assert.IsTrue(ui.GetComponentsInChildren<Text>().Any(t => t.text == "全螢幕閃光"));

            Assert.IsTrue(ui.ChooseSetting(2)); // off
            Assert.AreEqual(0f, VfxService.ScreenFlashStrength);
            Assert.AreEqual(0f, GameSession.Instance.Profile.screenFlash);
            var reloaded = GameSession.Instance.SaveService.Load();
            Assert.AreEqual(0f, reloaded.screenFlash, "stored in the save file");
            int before = VfxService.Instance.ScreenFlashes;
            SpellFx.Overdrive(P, P);
            Assert.AreEqual(before, VfxService.Instance.ScreenFlashes, "overdrive does not flash when off");

            Assert.IsTrue(ui.ChooseSetting(1)); // soft
            VfxService.Instance.ScreenFlash(Color.white, 0.5f, 0.2f);
            Assert.That(VfxService.Instance.ScreenFlashAlpha, Is.LessThanOrEqualTo(0.26f), "soft halves the flash");

            ui.TogglePause(); // Esc goes back to the pause menu
            yield return null;
            Assert.AreEqual("pause", ui.OpenPanel);
            ui.CloseModal();
            GameSession.Instance.Profile.screenFlash = 1f;
            VfxService.ScreenFlashStrength = 1f;
        }

        [UnityTest]
        public IEnumerator VillageCliffsAreTwoTilesTallWithCorners()
        {
            yield return Load("Village");
            var cliff = GameObject.Find("Cliff").GetComponent<Tilemap>();
            var face = cliff.GetTile(new Vector3Int(5, 34, 0)) as NeighborMaskTile;
            Assert.IsNotNull(face, "plateau face");
            Assert.AreEqual(NeighborMaskMode.EastWestNorth, face.mode);
            Assert.AreSame(face, cliff.GetTile(new Vector3Int(5, 35, 0)), "plateau face is two rows tall");
            Assert.AreNotSame(cliff.GetSprite(new Vector3Int(5, 35, 0)), cliff.GetSprite(new Vector3Int(5, 34, 0)), "upper and lower rows differ");
            Assert.AreSame(face, cliff.GetTile(new Vector3Int(30, 30, 0)), "ledge face grew down to two rows");
            Assert.AreSame(face, cliff.GetTile(new Vector3Int(30, 31, 0)));
            Assert.AreNotSame(cliff.GetSprite(new Vector3Int(30, 30, 0)), cliff.GetSprite(new Vector3Int(25, 30, 0)), "left corner sprite");
            Assert.AreNotSame(cliff.GetSprite(new Vector3Int(30, 30, 0)), cliff.GetSprite(new Vector3Int(37, 30, 0)), "right corner sprite");
            var collision = GameObject.Find("Collision").GetComponent<Tilemap>();
            for (int x = 25; x <= 37; x++)
                Assert.IsNotNull(collision.GetTile(new Vector3Int(x, 30, 0)), $"ledge foot blocks at x {x}");
            Assert.IsTrue(cliff.GetTile(new Vector3Int(17, 35, 0)) is NeighborMaskTile wf && wf != face, "waterfall spans both rows");
        }

        [Test]
        public void SpellEffectArtIsTheRedrawnVersion()
        {
            var db = GameDatabase.Instance;
            int Frames(string n) => db.Sprites(n)?.frames?.Length ?? 0;
            Assert.AreEqual(12, Frames("VFX_blast"));
            Assert.AreEqual(10, Frames("VFX_frost_nova"));
            Assert.AreEqual(9, Frames("VFX_sky_bolt"));
            Assert.AreEqual(8, Frames("VFX_wind_blades"));
            Assert.AreEqual(8, Frames("VFX_shockwave"));
            foreach (string name in new[]
            {
                "fireball", "ice_shard", "frost_blade", "homing", "starfire", "magic_bolt", "charge", "slash",
                "spell_aura", "vortex", "wind_funnel", "fire_tornado", "spell_pulse", "steam", "overload",
                "shatter", "spell_glow",
            })
            {
                var set = db.Sprites("VFX_" + name);
                Assert.IsNotNull(set, name);
                Assert.AreEqual("VFX_" + name, set.name, "uses the polished animation for " + name);
                Assert.IsNotEmpty(set.frames, name);
                foreach (var frame in set.frames)
                {
                    Assert.IsNotNull(frame, name);
                    Assert.AreEqual(32, frame.pixelsPerUnit, name);
                    Assert.AreEqual(FilterMode.Point, frame.texture.filterMode, name);
                }
            }
        }
    }
}
