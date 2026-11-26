using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Starbind.Tests
{
    /// <summary>Real Play-mode frames through the game camera (Pixel Perfect 480x270 upscaled to
    /// 1920x1080) with the HUD composited from a UI camera, into docs/evidence/final/. Category
    /// "Evidence" so normal runs can filter it.</summary>
    [Category("Evidence")]
    public class GameEvidenceCapture : PlayTestBase
    {
        static string outDir;
        static string OutDir => outDir ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/final"));

        Camera uiCam;
        Canvas canvas;

        IEnumerator Shot(string name, int settleFrames = 4)
        {
            Directory.CreateDirectory(OutDir);
            var cam = Camera.main;
            var world = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            var ui = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            canvas = GameSession.Instance.UI.GetComponent<Canvas>();
            if (uiCam == null)
            {
                var go = new GameObject("EvidenceUICamera");
                uiCam = go.AddComponent<Camera>();
                uiCam.orthographic = true;
                uiCam.clearFlags = CameraClearFlags.SolidColor;
                uiCam.backgroundColor = new Color(0, 0, 0, 0);
                uiCam.cullingMask = 1 << 5;
                uiCam.enabled = false;
                go.transform.position = new Vector3(5000, 5000, -10);
            }
            SetLayer(canvas.transform, 5);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 5;
            int mask = cam.cullingMask;
            cam.cullingMask = mask & ~(1 << 5);
            uiCam.targetTexture = ui;
            uiCam.enabled = true;
            cam.targetTexture = world;
            for (int i = 0; i < settleFrames; i++)
                yield return null;
            var a = Read(world);
            var b = Read(ui);
            cam.targetTexture = null;
            uiCam.targetTexture = null;
            uiCam.enabled = false;
            cam.cullingMask = mask;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            for (int i = 0; i < pa.Length; i++)
            {
                float al = pb[i].a / 255f;
                if (al <= 0) continue;
                pa[i] = new Color32((byte)(pb[i].r * al + pa[i].r * (1 - al)), (byte)(pb[i].g * al + pa[i].g * (1 - al)), (byte)(pb[i].b * al + pa[i].b * (1 - al)), 255);
            }
            a.SetPixels32(pa);
            a.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), a.EncodeToPNG());
            Debug.Log("[Evidence] wrote " + name);
            Object.Destroy(a);
            Object.Destroy(b);
            world.Release();
            ui.Release();
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayer(t.GetChild(i), layer);
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        IEnumerator Overview(string name, float w, float h)
        {
            var go = new GameObject("OverviewCamera");
            var c = go.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = h / 2f;
            c.transform.position = new Vector3(w / 2f, h / 2f, -10f);
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.cullingMask = ~(1 << 5);
            var rt = new RenderTexture((int)(w * 32), (int)(h * 32), 24) { filterMode = FilterMode.Point };
            c.targetTexture = rt;
            for (int i = 0; i < 3; i++)
                yield return null;
            var t = Read(rt);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), t.EncodeToPNG());
            c.targetTexture = null;
            rt.Release();
            Object.Destroy(go);
            Object.Destroy(t);
        }

        /// <summary>Real-time wait that auto-picks level-up cards (kills during captures level the run up,
        /// and the card screen pauses scaled time).</summary>
        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                var ui = GameSession.Instance.UI;
                if (ui.OpenPanel == "upgrade" && !holdCards)
                    ui.ChooseCard(0);
                yield return null;
            }
        }

        static bool holdCards;

        IEnumerator At(Vector2 p)
        {
            player.Teleport(p);
            CameraFollow2D.Main.SnapToTarget();
            yield return Wait(0.4f);
            CameraFollow2D.Main.SnapToTarget();
        }

        /// <summary>Moonveil Forest (Witch World Studio): POI views with the HUD, a combat frame and a
        /// half-resolution whole-map overview, into docs/evidence/moonveil.</summary>
        [UnityTest]
        public IEnumerator CaptureMoonveil()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/moonveil"));
            Directory.CreateDirectory(outDir);
            try
            {
                yield return Load("MoonveilForest");
                var run = RunManager.Current;
                run.Director.Running = false;
                EnemySpawner.Instance.DespawnAll();
                player.Vitals.Health.InvulnerableUntil = 9999f;
                var views = new (string name, Vector2 at)[]
                {
                    ("M01_entrance_gate", new Vector2(77.5f, 9f)), ("M02_moon_altar", new Vector2(78f, 59f)),
                    ("M03_hidden_camp", new Vector2(31f, 37f)), ("M04_glimmer_lake", new Vector2(38f, 58.5f)),
                    ("M05_waterfall_bridge", new Vector2(96f, 64f)), ("M06_boss_stairs", new Vector2(130.5f, 78f)),
                    ("M07_boss_clearing", new Vector2(129f, 92f)), ("M08_crystal_grove", new Vector2(131f, 59f)),
                    ("M09_mushroom_valley", new Vector2(27f, 93f)), ("M10_ancient_tree", new Vector2(76f, 96f)),
                    ("M11_gathering_woods", new Vector2(109f, 39f)),
                };
                // first, before the boss clearing view starts the boss fight: combat readability on the road (references/02): mixed group, fire bolt + wind pull
                yield return At(new Vector2(77.5f, 40f));
                var sp = EnemySpawner.Instance;
                var c = new Vector2(80f, 42f);
                foreach (var id in new[] { "slime", "slime", "spore_shroom", "wisp", "root_guard" })
                    sp.SpawnAt(id, sp.RandomAround(c, 1.6f));
                yield return Wait(0.3f);
                player.Combat.CastSlot(0, c);
                yield return Wait(0.4f);
                player.Combat.CastSlot(1, c);
                yield return Wait(0.5f);
                yield return Shot("M12_combat_on_the_road", 2);
                EnemySpawner.Instance.DespawnAll();
                foreach (var (name, at) in views)
                {
                    yield return At(at);
                    yield return Shot(name, 3);
                }
                // Moonveil art pass 2: purple Eclipse Mushroom King (after its spawn puff clears), vine burn, lever
                yield return At(new Vector2(129f, 89.5f));
                yield return Wait(2.5f);
                yield return Shot("M13_boss_moonveil", 3);
                EnemySpawner.Instance.DespawnAll();
                var vine = Object.FindAnyObjectByType<BurnableVine>();
                yield return At((Vector2)vine.transform.position + new Vector2(0, -2.2f));
                yield return Shot("M14_vine_moonveil", 3);
                vine.Burn();
                yield return Wait(0.16f);
                yield return Shot("M15_vine_burning", 2);
                var sw = Object.FindAnyObjectByType<BridgeSwitch>();
                yield return At((Vector2)sw.transform.position + new Vector2(-2f, -1f));
                yield return Shot("M16_bridge_lever_off", 3);
                sw.GetComponent<Interactable>().Interact();
                yield return Wait(0.6f);
                yield return Shot("M17_bridge_lever_on", 3);
                // phone layout (mobile browser build): stick, thumb cluster, interact and menu buttons
                var touch = GameSession.Instance.UI.Touch;
                touch.SetActive(true);
                yield return At(new Vector2(78f, 40f));
                yield return Shot("M18_touch_controls", 3);
                touch.SetActive(false);
                yield return OverviewScaled("M00_overview_half", 160, 120, 16);
            }
            finally
            {
                outDir = null;
            }
        }

        /// <summary>Whole-map frame at `ppt` pixels per tile (32 = native) for large regions.</summary>
        IEnumerator OverviewScaled(string name, float w, float h, int ppt)
        {
            var go = new GameObject("OverviewCamera");
            var c = go.AddComponent<Camera>();
            c.orthographic = true;
            c.orthographicSize = h / 2f;
            c.transform.position = new Vector3(w / 2f, h / 2f, -10f);
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.cullingMask = ~(1 << 5);
            var rt = new RenderTexture((int)(w * ppt), (int)(h * ppt), 24) { filterMode = FilterMode.Point };
            c.targetTexture = rt;
            for (int i = 0; i < 3; i++)
                yield return null;
            var t = Read(rt);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), t.EncodeToPNG());
            c.targetTexture = null;
            rt.Release();
            Object.Destroy(go);
            Object.Destroy(t);
        }

        /// <summary>Chapters 2-5: whole-map overview, entrance with HUD, the mechanism in action and the boss
        /// telegraph, plus the star-plate region list and the finale's ending choice (docs/evidence/chapters).</summary>
        [UnityTest]
        public IEnumerator CaptureChapters()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/chapters"));
            Directory.CreateDirectory(outDir);
            try
            {
                var chapters = new[] { ("Mine", "mine", 64, 80), ("Lake", "lake", 80, 64), ("Observatory", "observatory", 64, 96), ("Citadel", "citadel", 96, 96) };
                string[] flags = { "defeat_mushroom_king", "defeat_crystal_golem", "defeat_lake_shadow", "defeat_star_guardian" };
                for (int i = 0; i < chapters.Length; i++)
                {
                    var (scene, id, w, h) = chapters[i];
                    var profile = new Starbind.Save.ProfileData { victories = i + 1 };
                    for (int k = 0; k <= i; k++)
                        profile.CompleteQuest(flags[k]);
                    yield return Load(scene, profile);
                    player.Vitals.Health.InvulnerableUntil = 9999f;
                    yield return Overview(id + "_00_overview", w, h);
                    yield return Wait(2.5f);
                    yield return Shot(id + "_01_entrance");
                    RunManager.Current.Director.Running = false;
                    EnemySpawner.Instance.DespawnAll();
                    switch (id)
                    {
                        case "mine":
                        {
                            var cart = Object.FindAnyObjectByType<MineCart>();
                            yield return At((Vector2)cart.transform.position + new Vector2(2.5f, -1.2f));
                            cart.GetComponent<Interactable>().Interact();
                            yield return Wait(1.8f);
                            yield return Shot(id + "_02_minecart_blast", 1);
                            var crystal = Object.FindObjectsByType<ConductiveCrystal>()[0];
                            yield return At((Vector2)crystal.transform.position + new Vector2(0, -1.5f));
                            foreach (var c in Object.FindObjectsByType<ConductiveCrystal>())
                                c.Light();
                            yield return Wait(0.3f);
                            yield return Shot(id + "_03_crystal_circuit");
                            break;
                        }
                        case "lake":
                        {
                            foreach (var c in Object.FindObjectsByType<ConductiveCrystal>().Where(c => c.Circuit == "lake_west"))
                                c.Light();
                            yield return At(new Vector2(22f, 36.5f));
                            yield return Wait(0.5f);
                            yield return Shot(id + "_02_light_bridge");
                            break;
                        }
                        case "observatory":
                        {
                            yield return At(new Vector2(47f, 22f));
                            yield return Wait(0.3f);
                            yield return Shot(id + "_02_beam_unsolved");
                            foreach (var m in Object.FindObjectsByType<BeamMirror>())
                                m.GetComponent<Interactable>().Interact();
                            yield return Wait(0.4f);
                            yield return Shot(id + "_03_beam_solved");
                            break;
                        }
                        case "citadel":
                        {
                            var seal = Object.FindObjectsByType<TrialSeal>().First(s => s.School == "月影");
                            yield return At((Vector2)seal.transform.position + new Vector2(0, -2.5f));
                            seal.Begin(null);
                            yield return Wait(2.2f);
                            yield return Shot(id + "_02_moon_trial");
                            break;
                        }
                    }
                    EnemySpawner.Instance.DespawnAll();
                    var arena = Object.FindAnyObjectByType<BossArena>();
                    yield return At(arena.BossSpawn + new Vector2(0, -4.5f));
                    arena.Begin();
                    yield return Wait(2.6f);
                    yield return Shot(id + "_04_boss");
                    if (id == "citadel")
                    {
                        RunManager.Current.Finish(true);
                        GameSession.Instance.UI.ContinueResults();
                        yield return Wait(0.3f);
                        yield return Shot("citadel_05_ending_choice");
                        GameSession.Instance.UI.CloseModal();
                    }
                    GameSession.Instance.ResetTransientState();
                }
                var p = new Starbind.Save.ProfileData { victories = 2 };
                p.CompleteQuest("defeat_mushroom_king");
                p.CompleteQuest("defeat_crystal_golem");
                yield return Load("Village", p);
                var plate = Object.FindAnyObjectByType<RegionSelector>();
                yield return At((Vector2)plate.transform.position + new Vector2(0, -1f));
                plate.GetComponent<Interactable>().Interact();
                yield return Wait(0.3f);
                yield return Shot("village_star_plate_regions");
                GameSession.Instance.UI.CloseModal();
                GameSession.Instance.UI.ToggleQuests();
                yield return Wait(0.3f);
                yield return Shot("village_quest_log");
                GameSession.Instance.UI.CloseModal();
            }
            finally
            {
                outDir = null;
            }
        }

        [UnityTest]
        public IEnumerator CaptureVillage()
        {
            yield return Load("Village");
            player.Build.Auto.Enabled = false;
            yield return At(new Vector2(24.2f, 34.6f));
            yield return Shot("V01_village_square_hud");
            // facing left (walk left a moment, then capture while still facing left)
            for (float t = 0; t < 0.3f; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(new Vector2(-1f, -0.6f));
                yield return null;
            }
            player.Motor.SetMoveInput(Vector2.zero);
            yield return Shot("V10_witch_facing_down_left", 2);
            GameSession.Instance.UI.ToggleMap();
            yield return Shot("V09_world_map", 2);
            GameSession.Instance.UI.ToggleMap();
            yield return At(new Vector2(9.0f, 15.6f));
            yield return Shot("V02_bridge_archer");
            yield return At(new Vector2(21.6f, 9.5f));
            yield return Shot("V03_south_road_priest");
            yield return At(new Vector2(36.0f, 12.0f));
            yield return Shot("V04_training_ground");
            // combat on the dummies: staff finisher + fire bolt explosion + steam burst
            yield return At(new Vector2(33.4f, 11.75f));
            player.Build.SetActive(1, "piercing_ice");
            player.Combat.CastSlot(0, new Vector2(37f, 12.2f));
            yield return Wait(0.3f);
            player.Combat.CastSlot(1, new Vector2(37f, 12.2f));
            yield return Wait(0.28f);
            yield return Shot("V05_fire_then_ice_steam_burst", 2);
            yield return At(new Vector2(37.0f, 10.85f));
            player.Combat.Staff(new Vector2(37f, 12.2f));
            yield return Wait(0.27f);
            player.Combat.Staff(new Vector2(37f, 12.2f));
            yield return Wait(0.29f);
            player.Combat.Staff(new Vector2(37f, 12.2f));
            yield return Wait(0.06f);
            yield return Shot("V06_staff_finisher", 2);
            var ella = Object.FindObjectsByType<Talkable>().First(t => t.Speaker == "艾拉");
            yield return At(ella.transform.position + new Vector3(0.9f, -0.6f));
            ella.GetComponent<Interactable>().Interact();
            yield return Shot("V07_npc_dialogue");
            GameSession.Instance.UI.CloseDialogue();
            yield return Overview("V00_village_overview_native_1536x1280", 48, 40);
        }

        [UnityTest]
        public IEnumerator CaptureWorkshop()
        {
            yield return Load("Workshop");
            yield return At(new Vector2(7f, 3.2f));
            yield return Shot("W01_workshop_interior");
            GameSession.Instance.UI.ShowLoadout();
            yield return Shot("W02_loadout_lectern");
            GameSession.Instance.UI.CloseModal();
            GameSession.Instance.Profile.starSand = 40;
            GameSession.Instance.UI.ShowResearch();
            yield return Shot("W03_research_desk");
            GameSession.Instance.UI.CloseModal();
            yield return Overview("W00_workshop_overview_native", 14, 10);
        }

        [UnityTest]
        public IEnumerator CaptureForestCombat()
        {
            yield return Load("MoonveilForest");
            var run = RunManager.Current;
            run.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return At(new Vector2(77.5f, 4f));
            yield return Shot("F01_forest_entrance_hud");

            // mixed group + fire bolt + wind pull -> fire tornado
            yield return At(new Vector2(77.5f, 40f));
            var sp = EnemySpawner.Instance;
            var c = new Vector2(77.5f, 43f);
            foreach (var id in new[] { "slime", "slime", "spore_shroom", "wisp", "root_guard", "slime" })
                sp.SpawnAt(id, sp.RandomAround(c, 1.6f));
            yield return Wait(0.3f);
            player.Combat.CastSlot(0, c);
            yield return Wait(0.45f);
            player.Combat.CastSlot(1, c);
            int tornado = 0;
            CombatWorld.Instance.Bus.ReactionTriggered += (r, p, h) => { if (r == ReactionType.FireTornado) tornado++; };
            yield return WaitUntilOrTimeout(() => tornado > 0, 2.5f);
            yield return Wait(0.25f);
            yield return Shot("F02_fire_wind_fire_tornado", 2);

            // overload + shatter storm on a fresh group
            sp.DespawnAll();
            yield return At(new Vector2(78f, 53.5f));
            var g = new Vector2(78f, 57f);
            var group = Enumerable.Range(0, 7).Select(i => sp.SpawnAt("root_guard", g + new Vector2((i % 4) * 1.0f - 1.5f, (i / 4) * 1.0f))).Where(e => e != null).ToList();
            var world = CombatWorld.Instance;
            foreach (var e in group)
                world.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 1, Element = ElementType.Ice, StatusDuration = 4, Source = HitSource.ActiveSpell }, e.Health);
            player.Build.SetActive(0, "chain_spark");
            player.Combat.CastSlot(0, g + new Vector2(0, 0.5f));
            yield return Wait(0.24f);
            yield return Shot("F03_ice_lightning_shatter_storm", 1);
            foreach (var e in group.Where(e => !e.Health.IsDead))
                world.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 1, Element = ElementType.Fire, StatusDuration = 4, Source = HitSource.ActiveSpell }, e.Health);
            yield return Wait(2.9f);
            player.Combat.CastSlot(0, g + new Vector2(0, 0.5f));
            yield return Wait(0.22f);
            yield return Shot("F04_fire_lightning_overload", 1);

            // overdrive + auto spells in a 60-enemy wave at the altar
            sp.DespawnAll();
            player.Build.AddAuto("frost_orbit");
            player.Combat.Resonance.ForceFill();
            player.Combat.TryOverdrive();
            string[] mix = { "slime", "spore_shroom", "wisp", "slime", "root_guard", "slime" };
            for (int i = 0; i < 70; i++)
                sp.SpawnAt(mix[i % mix.Length], sp.RandomAround(new Vector2(78f, 55f), 9f));
            yield return At(new Vector2(78f, 55f));
            yield return Wait(1.2f);
            yield return Shot("F05_wave_60_enemies_overdrive_autos");

            // elite telegraphs
            sp.DespawnAll();
            var elite = sp.SpawnAt("spore_elite", new Vector2(109f, 41.5f));
            yield return At(new Vector2(106.5f, 39.5f));
            yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 5f);
            yield return Wait(0.3f);
            yield return Shot("F06_elite_telegraph");
            sp.DespawnAll();
        }

        [UnityTest]
        public IEnumerator CaptureBossAndResults()
        {
            yield return Load("MoonveilForest");
            var run = RunManager.Current;
            run.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return At(new Vector2(129f, 90.5f));
            yield return WaitUntilOrTimeout(() => Enemy.ActiveBoss != null, 3f);
            var boss = Enemy.ActiveBoss;
            yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 6f);
            yield return Wait(0.35f);
            yield return Shot("F07_boss_phase1_telegraph");
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = boss.Health.Max * 0.7f, Source = HitSource.ActiveSpell }, boss.Health);
            yield return Wait(1.2f);
            yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 6f);
            yield return Wait(0.4f);
            yield return Shot("F08_boss_phase3");
            holdCards = true;
            run.Run.AddXp(RunState.XpToNext(run.Run.Level));
            yield return WaitUntilOrTimeout(() => GameSession.Instance.UI.OpenPanel == "upgrade", 2f);
            yield return Shot("F09_level_up_cards");
            GameSession.Instance.UI.ChooseCard(0);
            holdCards = false;
            boss.Health.InvulnerableUntil = 0;
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = boss.Health.Max, Source = HitSource.ActiveSpell }, boss.Health);
            yield return Wait(0.8f);
            yield return Shot("F10_boss_defeated_portal");
            run.Finish(true);
            yield return Shot("F11_results_victory");
            GameSession.Instance.UI.CloseModal();
            yield return OverviewScaled("F00_forest_overview_half", 160, 120, 16);
        }

        /// <summary>2026-11-21 polish: big spell effects, new level-up cards, the three grimoire pages,
        /// minimap objective arrow (screen flashes live on their own overlay canvas and are not in these frames).
        /// Shots settle 3 frames: with 1, text laid out for the Overlay canvas is drawn in Camera mode and looks cropped.</summary>
        [UnityTest]
        public IEnumerator CapturePolish()
        {
            ObjectiveTracker.ResetStatics();
            yield return Load("MoonveilForest");
            var run = RunManager.Current;
            run.Director.Running = false;
            var sp = EnemySpawner.Instance;
            sp.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            player.Build.Auto.Enabled = false;
            var c = new Vector2(77.5f, 43f);

            IEnumerator Group()
            {
                sp.DespawnAll();
                yield return At(new Vector2(77.5f, 39.8f));
                foreach (var id in new[] { "slime", "slime", "slime", "wisp" }) // root guards' red attack telegraphs would cover the spell
                    sp.SpawnAt(id, sp.RandomAround(c, 1.2f));
                foreach (var e in sp.AliveList) e.Health.InvulnerableUntil = 9999f;
                yield return Wait(0.2f);
            }

            yield return Group();
            player.Build.SetActive(0, "fire_bolt");
            player.Combat.CastSlot(0, c);
            yield return Wait(0.04f);
            yield return Shot("S01_fire_bolt_cast_trail", 3);
            yield return WaitUntilOrTimeout(() => VFX.SpellFx.ImpactEffects > 0, 1.5f);
            yield return Wait(0.06f);
            yield return Shot("S02_fire_bolt_blast", 3);

            yield return Group();
            player.Build.SetActive(0, "chain_spark");
            int impacts = VFX.SpellFx.ImpactEffects;
            player.Combat.CastSlot(0, c);
            yield return WaitUntilOrTimeout(() => VFX.SpellFx.ImpactEffects > impacts, 1.5f);
            yield return Shot("S03_chain_spark_sky_lightning", 3);

            yield return Group();
            player.Build.SetActive(0, "piercing_ice");
            impacts = VFX.SpellFx.ImpactEffects;
            yield return At(new Vector2(77.5f, 38.5f)); // a little further back so the shard flies before it hits
            player.Combat.CastSlot(0, c);
            yield return WaitUntilOrTimeout(() => VFX.SpellFx.ImpactEffects > impacts, 1.5f);
            yield return Shot("S04_piercing_ice_frost_nova", 3);

            yield return Group();
            player.Build.SetActive(0, "wind_pull");
            player.Combat.CastSlot(0, c);
            yield return Wait(0.7f);
            yield return Shot("S05_wind_pull_vortex", 3);

            yield return Group();
            player.Combat.Resonance.ForceFill();
            player.Combat.TryOverdrive();
            yield return Wait(0.06f);
            yield return Shot("S06_overdrive_sky_strikes", 3);
            yield return Wait(1.0f);

            // the redrawn flipbooks side by side (display only, no enemies): blast, frost nova, sky bolt, wind blades
            sp.DespawnAll();
            yield return At(new Vector2(77.5f, 39.8f));
            var vfx = VFX.VfxService.Instance;
            var o = P + new Vector2(0, 2.6f);
            vfx.Play("VFX_sky_bolt", o + new Vector2(1.2f, -0.8f), 0, 1.2f);
            yield return Wait(0.05f);
            vfx.Play("VFX_blast", o + new Vector2(-3.6f, 0), 0, 1.6f);
            vfx.Play("VFX_frost_nova", o + new Vector2(4.4f, -0.6f), 0, 1.4f);
            vfx.Play("VFX_wind_blades", o + new Vector2(-1.2f, -2.6f), 0, 1.4f);
            yield return Wait(0.17f);
            yield return Shot("S07_redrawn_effect_art", 3);
            yield return Wait(0.6f);

            // level-up cards (held open for the shot)
            sp.DespawnAll();
            holdCards = true;
            run.Run.AddXp(RunState.XpToNext(run.Run.Level));
            yield return Wait(0.6f);
            yield return Shot("U01_level_up_cards", 2);
            holdCards = false;
            GameSession.Instance.UI.ChooseCard(0);
            yield return Wait(0.2f);

            var ui = GameSession.Instance.UI;
            ui.ToggleGrimoire();
            for (int tab = 0; tab < 3; tab++)
            {
                ui.Grimoire.Show(tab);
                yield return Wait(0.1f);
                yield return Shot($"U0{tab + 2}_grimoire_page{tab + 1}", 2);
            }
            ui.CloseModal();

            // settings: full-screen flash level (from the pause menu)
            ui.TogglePause();
            yield return Wait(0.1f);
            yield return Shot("U05_pause_menu");
            ui.ShowSettings();
            ui.ChooseSetting(1);
            yield return Wait(0.1f);
            yield return Shot("U06_settings_screen_flash");
            ui.ChooseSetting(0);
            ui.CloseModal();

            // 2-tile-tall village cliffs with corners: plateau + waterfall, and the ledge below the fountain
            yield return Load("Village");
            yield return At(new Vector2(9.0f, 31.5f));
            yield return Wait(0.3f);
            yield return Shot("V11_plateau_cliff_two_tiles", 3);
            yield return At(new Vector2(31.5f, 27.6f));
            yield return Wait(0.3f);
            yield return Shot("V12_ledge_cliff_corners", 3);
        }
    }
}
