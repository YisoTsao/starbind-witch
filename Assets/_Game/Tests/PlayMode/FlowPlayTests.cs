using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    /// <summary>Scene flow and world interactions: village bridge / NPC / workshop round trip, forest
    /// bridge lever and vines, spawn cap with a frame-time measurement, boss phases, victory and death.</summary>
    public class FlowPlayTests : PlayTestBase
    {
        static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/final"));

        [UnityTest]
        public IEnumerator VillageIs48x40WithAllAreas()
        {
            yield return Load("Village");
            var follow = CameraRig.CameraFollow2D.Main;
            Assert.AreEqual(new Rect(0, 0, 48, 40), follow.Bounds);
            Assert.AreEqual(4, Object.FindObjectsByType<TrainingDummy>().Length, "training ground dummies");
            Assert.AreEqual(17, Object.FindObjectsByType<Talkable>().Count(t => t.name == "Talk"), "Ella, Kain, Lia, Lorne, the prince, 12 villagers");
            Assert.AreEqual(12, Object.FindObjectsByType<Player.NpcWander>().Length, "every villager strolls");
            Assert.IsNotNull(GameObject.Find("Bridge"));
            Assert.IsNotNull(Object.FindAnyObjectByType<AreaExit>());
            Assert.IsNotNull(SpawnPoint.Find("spawn_from_forest"));
        }

        [UnityTest]
        public IEnumerator VillagersStrollWithinRangeAndCharactersUseReferenceSheets()
        {
            yield return Load("Village");
            var witchAnim = player.GetComponentInChildren<Player.CharacterAnimator>();
            yield return null;
            Assert.IsTrue(witchAnim.IsV2, "witch uses the 36-frame sheet cut from the chibi mage reference");
            var wanderers = Object.FindObjectsByType<Player.NpcWander>();
            var start = wanderers.ToDictionary(w => w, w => (Vector2)w.transform.position);
            var sheets = new HashSet<string>();
            foreach (var w in wanderers)
                {
                var n = w.GetComponentInChildren<Player.CharacterAnimator>().GetComponent<SpriteRenderer>().sprite.name; // "<sheet>_<frame>"; the first child renderer is the shared shadow
                sheets.Add(n.Substring(0, n.LastIndexOf('_')));
            }
            Assert.AreEqual(12, sheets.Count, "each villager has its own look");
            bool sawWalkFrame = false;
            for (float t = 0; t < 6f; t += Time.deltaTime)
            {
                foreach (var w in wanderers)
                {
                    var a = w.GetComponentInChildren<Player.CharacterAnimator>();
                    if (w.Walking && a.CurrentFrame % 12 >= 2 && a.CurrentFrame % 12 <= 6)
                        sawWalkFrame = true;
                }
                yield return null;
            }
            Assert.IsTrue(sawWalkFrame, "walking villagers play the walk cycle");
            Assert.That(wanderers.Count(w => Vector2.Distance(start[w], w.transform.position) > 0.3f), Is.GreaterThanOrEqualTo(4), "villagers actually move");
            foreach (var w in wanderers)
                Assert.That(Vector2.Distance(w.Home, w.transform.position), Is.LessThanOrEqualTo(w.Radius + 0.15f), w.name + " stays near home");

            // walking up to a villager stops it and talking plays its greeting (action frames 7-11)
            var v = wanderers[0];
            yield return TeleportTo((Vector2)v.transform.position + new Vector2(0, -1.0f));
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(v.Walking, "villager stops for the witch");
            v.GetComponentInChildren<Talkable>().GetComponent<Interactable>().Interact();
            yield return null;
            yield return null;
            Assert.That(v.GetComponentInChildren<Player.CharacterAnimator>().CurrentFrame % 12, Is.GreaterThanOrEqualTo(7), "greeting action");
        }

        [UnityTest]
        public IEnumerator EightWayFacingShowsLeftForEveryLeftwardHeading()
        {
            yield return Load("Village");
            var anim = player.GetComponentInChildren<Player.CharacterAnimator>();
            var sr = anim.GetComponent<SpriteRenderer>();
            // heading -> expected (direction, faces left); diagonals use the side frames
            var cases = new (Vector2 dir, int expectDir, bool left)[]
            {
                (Vector2.left, 2, true), (new Vector2(-1, 1), 2, true), (new Vector2(-1, -1), 2, true),
                (Vector2.right, 2, false), (new Vector2(1, 1), 2, false), (new Vector2(1, -1), 2, false),
                (Vector2.up, 1, false), (Vector2.down, 0, false),
            };
            foreach (var c in cases)
            {
                yield return TeleportTo(new Vector2(30.5f, 26.5f));
                yield return Hold(c.dir.normalized, 0.25f);
                Assert.AreEqual(c.expectDir, anim.CurrentDirection, "frame set for heading " + c.dir);
                Assert.AreEqual(c.left, anim.FacingLeft, "faces left for heading " + c.dir);
                Assert.AreEqual(c.left, sr.flipX, "renderer flipped for heading " + c.dir);
                Assert.AreEqual(c.expectDir, anim.CurrentFrame / 12, "v2 frame index row for heading " + c.dir);
            }
            // NPCs share the animator: a villager facing the witch from up-left faces left
            var v = Object.FindObjectsByType<Player.NpcWander>()[0];
            var va = v.GetComponentInChildren<Player.CharacterAnimator>();
            yield return TeleportTo((Vector2)v.transform.position + new Vector2(-1.0f, 0.8f));
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(va.FacingLeft && va.CurrentDirection == 2, "villager turns left toward the witch at up-left");
        }

        /// <summary>Checks the rendered pixels, not just SpriteRenderer.flipX: the character shader once
        /// ignored flipX, so the flag was set but the witch still looked right.</summary>
        [UnityTest]
        public IEnumerator FlippedCharacterIsMirroredOnScreen()
        {
            yield return Load("Village");
            foreach (var w in Object.FindObjectsByType<Player.NpcWander>())
                w.gameObject.SetActive(false);
            var motes = Object.FindFirstObjectByType<VFX.AmbientMotes>();
            if (motes != null)
                motes.gameObject.SetActive(false);
            yield return TeleportTo(new Vector2(30.5f, 26.5f));
            var anim = player.GetComponentInChildren<Player.CharacterAnimator>();
            var sr = anim.GetComponent<SpriteRenderer>();
            anim.Facing = Vector2.right;
            yield return null;
            anim.enabled = false; // hold one frame
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            sr.flipX = false;
            yield return null;
            yield return null;
            var right = Grab(rt);
            sr.flipX = true;
            yield return null;
            yield return null;
            var left = Grab(rt);
            sr.enabled = false;
            yield return null;
            yield return null;
            var bg = Grab(rt);
            sr.enabled = true;
            Vector3 sp = cam.WorldToScreenPoint(sr.transform.position);
            cam.targetTexture = null;
            rt.Release();
            anim.enabled = true;

            // silhouettes (pixels that differ from the background) compared plain vs mirrored around the pivot
            int cx = Mathf.RoundToInt(sp.x), y0 = Mathf.RoundToInt(sp.y) - 8, half = 120, hgt = 230;
            bool On(Texture2D t, int x, int y)
            {
                Color p = t.GetPixel(x, y), q = bg.GetPixel(x, y);
                return Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b) > 0.12f;
            }
            float IoU(int shift, bool mirror)
            {
                int inter = 0, uni = 0;
                for (int y = y0; y < y0 + hgt; y++)
                    for (int dx = -half; dx < half; dx++)
                    {
                        bool r = On(right, cx + dx, y), l = On(left, mirror ? cx - 1 - dx + shift : cx + dx, y);
                        if (r && l) inter++;
                        if (r || l) uni++;
                    }
                return uni == 0 ? 0 : inter / (float)uni;
            }
            float plain = IoU(0, false);
            float mirrored = Mathf.Max(IoU(-4, true), Mathf.Max(IoU(0, true), IoU(4, true)));
            Object.Destroy(right);
            Object.Destroy(left);
            Object.Destroy(bg);
            Assert.That(plain, Is.LessThan(0.9f), $"flipX changes the drawn silhouette (overlap {plain:F2})");
            Assert.That(mirrored, Is.GreaterThan(0.85f), $"left-facing render is the mirror image of right-facing (mirrored overlap {mirrored:F2}, plain {plain:F2})");
            Assert.That(mirrored, Is.GreaterThan(plain + 0.05f));
        }

        static Texture2D Grab(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        [UnityTest]
        public IEnumerator MiniMapAndWorldMapShowTheScene()
        {
            yield return Load("Village");
            yield return null;
            yield return null;
            var ui = GameSession.Instance.UI;
            var mini = ui.Hud.MiniMap;
            Assert.IsNotNull(mini, "minimap on the HUD");
            var d = mini.Shown;
            Assert.IsNotNull(d, "village map built");
            Assert.AreEqual(48 * UI.MapData.Px, d.Texture.width);
            Assert.AreEqual(40 * UI.MapData.Px, d.Texture.height);
            Assert.That(d.Pois.Count(p => p.kind == "villager"), Is.EqualTo(12), "villagers on the map");
            Assert.That(d.Pois.Count(p => p.kind == "npc"), Is.GreaterThanOrEqualTo(5), "named NPCs (Ella, Kain, Lia, Lorne, prince)");
            Assert.That(d.Pois.Any(p => p.kind == "exit" && p.label == "月影森林"), "forest exit");
            Assert.That(d.Pois.Any(p => p.kind == "door"), "cottage door");
            Assert.That(d.Pois.Any(p => p.kind == "star"), "star plate");
            // water, path and grass get different colours
            var tex = d.Texture;
            Color water = tex.GetPixel(13 * 4 + 2, 10 * 4 + 2), path = tex.GetPixel(21 * 4 + 2, 5 * 4 + 2), grass = tex.GetPixel(30 * 4 + 2, 28 * 4 + 1); // (30, 30) is the ledge cliff since it became 2 tiles tall
            Assert.That(water.b, Is.GreaterThan(water.g), "water is blue");
            Assert.That(path.r, Is.GreaterThan(path.b + 0.2f), "path is sandy");
            Assert.That(grass.g, Is.GreaterThan(grass.r), "grass is green");
            // the minimap follows the witch and the arrow turns with her
            yield return TeleportTo(new Vector2(30.5f, 26.5f));
            yield return Hold(Vector2.left, 0.2f);
            yield return null;
            Assert.That(Mathf.Abs(mini.PlayerMarker.localEulerAngles.z - 90f), Is.LessThan(1f), "arrow points left");
            // M opens the world map (pauses gameplay), M again closes it
            ui.ToggleMap();
            Assert.AreEqual("map", ui.OpenPanel);
            Assert.IsTrue(ui.BlocksGameplay);
            Assert.That(UI.WorldMapPanel.LastScale, Is.GreaterThanOrEqualTo(2), "map drawn at an integer scale");
            yield return null;
            ui.ToggleMap();
            Assert.IsNull(ui.OpenPanel);
            // the HUD uses the pixel frames
            var vit = ui.Hud.transform.Find("Vitals").GetComponent<UnityEngine.UI.Image>();
            Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, vit.type);
            Assert.That(vit.sprite.name, Does.StartWith("UI_panel"));
        }

        [UnityTest]
        public IEnumerator MiniMapShowsForestEnemies()
        {
            yield return Load("Forest");
            yield return null;
            var mini = GameSession.Instance.UI.Hud.MiniMap;
            Assert.IsNotNull(mini.Shown, "forest map built");
            Assert.AreEqual(64 * UI.MapData.Px, mini.Shown.Texture.width);
            var e = EnemySpawner.Instance.SpawnAt("slime", (Vector2)player.transform.position + new Vector2(3, 0));
            Assert.IsNotNull(e);
            yield return null;
            yield return null;
            Assert.That(mini.VisibleEnemyMarkers, Is.GreaterThanOrEqualTo(1), "enemy dots on the minimap");
        }

        [UnityTest]
        public IEnumerator VillageIsDressedWithDecorLightsAndMotes()
        {
            yield return Load("Village");
            yield return null;
            var decals = GameObject.Find("Decals");
            Assert.IsNotNull(decals, "ground decal root");
            int dec = decals.GetComponentsInChildren<SpriteRenderer>().Count(r => r.sprite != null && r.sprite.name.StartsWith("DEC_"));
            Assert.That(dec, Is.GreaterThanOrEqualTo(200), "grass tufts, flowers, patches, lily pads");
            foreach (var r in decals.GetComponentsInChildren<SpriteRenderer>())
                Assert.IsNull(r.GetComponent<Collider2D>(), "decals never block movement: " + r.name);
            var names = Object.FindObjectsByType<SpriteRenderer>().Select(r => r.sprite != null ? r.sprite.name : "").ToList();
            foreach (var witchProp in new[] { "PROP_cauldron", "PROP_herb_rack", "PROP_lantern_post", "PROP_pumpkin", "PROP_crystal_cluster", "PROP_candles" })
                Assert.IsTrue(names.Any(n => n.StartsWith(witchProp)), witchProp + " placed in the village");
            Assert.IsFalse(names.Any(n => n.StartsWith("PROP_village_round") || n.StartsWith("PROP_lamp_")), "procedural round trees / plain lamps replaced");
            var glows = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>().Count(l => l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Point && l.name == "Glow");
            Assert.That(glows, Is.GreaterThanOrEqualTo(12), "lanterns, windows, crystals and mushrooms glow");
            var motes = Object.FindFirstObjectByType<VFX.AmbientMotes>();
            Assert.IsNotNull(motes);
            Assert.That(motes.Count, Is.GreaterThan(0));
            // the stroll test's villagers still have room to walk with the new props
            var start = Object.FindObjectsByType<Player.NpcWander>().ToDictionary(w => w, w => (Vector2)w.transform.position);
            yield return new WaitForSeconds(6f);
            Assert.That(start.Count(kv => Vector2.Distance(kv.Value, kv.Key.transform.position) > 0.3f), Is.GreaterThanOrEqualTo(4));
        }

        [UnityTest]
        public IEnumerator BridgeCrossesTheRiverButWaterBlocks()
        {
            yield return Load("Village");
            yield return TeleportTo(new Vector2(10.5f, 15.6f));
            yield return Hold(Vector2.right, 1.6f);
            Assert.That(P.x, Is.GreaterThan(16.2f), "walked across the bridge (river x 12-15)");
            yield return TeleportTo(new Vector2(13.5f, 15.7f));
            yield return Hold(Vector2.down, 1.0f);
            Assert.That(P.y, Is.GreaterThan(14.9f), "bridge rail keeps the witch out of the river");
            yield return TeleportTo(new Vector2(10.4f, 13.5f));
            yield return Hold(Vector2.right, 1.2f);
            Assert.That(P.x, Is.LessThan(12.0f), "river blocks next to the bridge");
        }

        [UnityTest]
        public IEnumerator NpcTalkOpensDialogueAndRecordsQuest()
        {
            yield return Load("Village");
            var ella = Object.FindObjectsByType<Talkable>().First(t => t.Speaker == "艾拉");
            ella.GetComponent<Interactable>().Interact();
            yield return null;
            Assert.IsTrue(GameSession.Instance.UI.DialogueOpen);
            Assert.IsTrue(GameSession.Instance.Profile.HasQuest("talked_ella"));
            Assert.IsTrue(GameSession.Instance.UI.BlocksGameplay, "movement input blocked while talking");
        }

        [UnityTest]
        public IEnumerator CottageDoorToWorkshopAndBack()
        {
            yield return Load("Village");
            yield return TeleportTo(new Vector2(24.53f, 36.4f));
            Assert.IsTrue(player.Interactor.TryInteract());
            yield return WaitForScene("Workshop");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_village").transform.position), Is.LessThan(0.1f));
            yield return TeleportTo(new Vector2(7f, 3.5f));
            Assert.IsTrue(player.Interactor.TryInteract());
            Assert.AreEqual("loadout", GameSession.Instance.UI.OpenPanel, "grimoire lectern");
            var session = GameSession.Instance;
            Assert.IsFalse(session.Profile.HasQuest("configure_first_spell"));
            var spellButton = session.UI.GetComponentsInChildren<UnityEngine.UI.Button>()
                .First(button => button.name == "piercing_ice");
            spellButton.onClick.Invoke();
            Assert.IsTrue(session.SaveService.Load().HasQuest("configure_first_spell"));
            GameSession.Instance.UI.CloseModal();
            yield return TeleportTo(new Vector2(2.4f, 3.7f));
            Assert.IsTrue(player.Interactor.TryInteract());
            Assert.AreEqual("research", GameSession.Instance.UI.OpenPanel, "research desk");
            GameSession.Instance.UI.CloseModal();
            yield return TeleportTo(new Vector2(7f, 1.25f));
            Assert.AreEqual("workshop_door", player.Interactor.Current?.InteractionId);
            Assert.IsTrue(player.Interactor.TryInteract());
            yield return WaitForScene("Village");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_workshop").transform.position), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator WorkshopWallsBlock()
        {
            yield return Load("Workshop");
            yield return TeleportTo(new Vector2(7f, 2.5f));
            yield return Hold(Vector2.down, 1.2f);
            Assert.That(P.y, Is.GreaterThan(0.9f), "bottom wall");
            yield return TeleportTo(new Vector2(8.5f, 6.5f));
            yield return Hold(Vector2.up, 1.2f);
            Assert.That(P.y, Is.LessThan(8.0f), "back wall");
        }

        [UnityTest]
        public IEnumerator ResearchSpendsMaterialsAndUnlocksSpell()
        {
            yield return Load("Workshop");
            var s = GameSession.Instance;
            s.Profile.starSand = 30;
            float hp0 = player.Vitals.Health.Max;
            Assert.IsTrue(s.TryResearch("hp"));
            Assert.AreEqual(22, s.Profile.starSand);
            Assert.AreEqual(hp0 + 10f, player.Vitals.Health.Max, 0.01f);
            Assert.IsFalse(s.Profile.HasQuest("research_after_victory"));
            s.Profile.victories = 1;
            Assert.IsFalse(s.TryResearch("thunder_pulse"));
            Assert.IsFalse(s.Profile.HasQuest("research_after_victory"));
            Assert.IsTrue(s.TryResearch("chain_spark"));
            Assert.IsTrue(s.SaveService.Load().HasQuest("research_after_victory"));
            Assert.IsTrue(s.Profile.IsUnlocked("chain_spark"));
            Assert.IsFalse(s.TryResearch("thunder_pulse"), "needs a core shard");
            Assert.IsTrue(File.Exists(s.SaveService.Path), "research saved");
        }

        // ------------------------------------------------------------------ forest
        IEnumerator LoadForestQuiet()
        {
            yield return Load("Forest");
            yield return null;
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrokenBridgeBlocksUntilLeverAndVinesBurn()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 46.2f));
            yield return Hold(Vector2.up, 1.2f);
            Assert.That(P.y, Is.LessThan(48.0f), "broken bridge gap blocks");
            var lever = Object.FindAnyObjectByType<BridgeSwitch>();
            lever.GetComponent<Interactable>().Interact();
            Assert.IsTrue(lever.Repaired);
            yield return Hold(Vector2.up, 1.6f);
            Assert.That(P.y, Is.GreaterThan(51.0f), "crossed the repaired bridge");

            var vine = Object.FindAnyObjectByType<BurnableVine>();
            var h = vine.GetComponent<Health>();
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 5, Source = HitSource.Staff }, h);
            Assert.IsFalse(vine.Burnt, "staff does not burn vines");
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 5, Element = ElementType.Fire, StatusDuration = 2, Source = HitSource.ActiveSpell }, h);
            Assert.IsTrue(vine.Burnt, "fire burns vines");
        }

        [UnityTest]
        public IEnumerator AltarStartsAWaveAndClearingRewards()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 36.5f));
            var altar = Object.FindAnyObjectByType<StarAltar>();
            altar.GetComponent<Interactable>().Interact();
            Assert.IsTrue(altar.Used);
            var director = RunManager.Current.Director;
            yield return WaitUntilOrTimeout(() => director.WaveRemainingToSpawn == 0, 6f);
            Assert.That(EnemySpawner.Instance.Alive, Is.GreaterThan(15), "wave spawned");
            foreach (var e in EnemySpawner.Instance.AliveList.ToList())
                e.Health.Kill();
            yield return WaitUntilOrTimeout(() => altar.Cleared, 2f);
            Assert.IsTrue(altar.Cleared);
            Assert.That(RunManager.Current.Run.PendingLevelUps + (GameSession.Instance.UI.OpenPanel == "upgrade" ? 1 : 0), Is.GreaterThanOrEqualTo(1), "bonus upgrade");
            Assert.IsTrue(RunManager.Current.Director.EliteSpawned, "elite appears after the wave");
        }

        [UnityTest]
        public IEnumerator SpawnCapHoldsAtSixtyAndFrameTimeIsMeasured()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 39f));
            var spawner = EnemySpawner.Instance;
            string[] mix = { "slime", "spore_shroom", "wisp", "slime", "root_guard" };
            for (int i = 0; i < 80; i++)
                spawner.SpawnAt(mix[i % mix.Length], spawner.RandomAround(P, 7f));
            Assert.AreEqual(EnemySpawner.MaxAlive, spawner.Alive);
            Assert.AreEqual(20, spawner.RejectedByCap);
            // auto spells + orbit on, so the measurement includes combat
            player.Build.AddAuto("frost_orbit");
            int startAlive = spawner.Alive;
            var frames = new List<float>();
            float end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                frames.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            frames.Sort();
            float avg = frames.Average(), p95 = frames[(int)(frames.Count * 0.95f)], max = frames[frames.Count - 1];
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence, "perf_60_enemies.json"),
                $"{{\"enemies_at_start\":{startAlive},\"enemies_at_end\":{spawner.Alive},\"frames\":{frames.Count},\"avg_ms\":{avg:0.00},\"p95_ms\":{p95:0.00},\"max_ms\":{max:0.00}," +
                $"\"vfx_live\":{VFX.VfxService.Instance.LiveFlipbooks},\"note\":\"batch-mode editor PlayMode on this Mac, Pixel Perfect 480x270; not a player build\"}}");
            Debug.Log($"[Perf] 60 enemies: avg {avg:0.00} ms, p95 {p95:0.00} ms, max {max:0.00} ms over {frames.Count} frames");
            Assert.That(spawner.Alive, Is.LessThanOrEqualTo(EnemySpawner.MaxAlive));
        }

        [UnityTest]
        public IEnumerator BossHasThreePhasesTelegraphsAndVictoryReturnsToVillage()
        {
            yield return Load("Village");
            var forestExit = Object.FindObjectsByType<AreaExit>(FindObjectsSortMode.None)
                .Single(exit => exit.TargetScene == "Forest");
            yield return TeleportTo(forestExit.Area.center);
            yield return WaitForScene("Forest");
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            var h = player.Vitals.Health;
            h.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 55.6f));
            yield return WaitUntilOrTimeout(() => Enemy.ActiveBoss != null, 2f);
            var boss = Enemy.ActiveBoss;
            Assert.IsNotNull(boss, "entering the arena starts the boss");
            var brain = (BossBrain)boss.Brain;
            Assert.AreEqual(1, brain.Phase);
            yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 5f);
            Assert.That(Telegraph.Active, Is.GreaterThan(0), "attacks are telegraphed");
            var world = CombatWorld.Instance;
            DamageEvent Big(float amount) => new DamageEvent { CastId = CombatIds.NewCast(), Amount = amount, Source = HitSource.ActiveSpell };
            world.Damage(Big(boss.Health.Max * 0.35f), boss.Health);
            yield return null;
            Assert.AreEqual(2, brain.Phase);
            boss.Health.InvulnerableUntil = 0;
            world.Damage(Big(boss.Health.Max * 0.35f), boss.Health);
            yield return null;
            Assert.AreEqual(3, brain.Phase);
            boss.Health.InvulnerableUntil = 0;
            world.Damage(Big(boss.Health.Max), boss.Health);
            yield return null;
            Assert.IsTrue(RunManager.Current.Run.BossDefeated);
            Assert.IsNotNull(Object.FindAnyObjectByType<ReturnPortal>(), "return portal");
            Object.FindAnyObjectByType<ReturnPortal>().GetComponent<Interactable>().Interact();
            Assert.AreEqual("results", GameSession.Instance.UI.OpenPanel);
            Assert.AreEqual(1, GameSession.Instance.Profile.victories);
            Assert.That(GameSession.Instance.Profile.coreShards, Is.GreaterThanOrEqualTo(3));
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            yield return WaitForScene("Village");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_forest").transform.position), Is.LessThan(0.1f));
            var disk = new Starbind.Save.SaveService(TempSaves);
            var restored = disk.Load();
            Assert.AreEqual(1, restored.victories);
            Assert.AreEqual(GameSession.Instance.Profile.coreShards, restored.coreShards);
            Assert.IsFalse(disk.LoadedFromBackup);
        }

        [UnityTest]
        public IEnumerator DeathShowsResultsKeepsHalfMaterialsAndReturnsToWorkshop()
        {
            yield return LoadForestQuiet();
            var run = RunManager.Current;
            run.Run.StarSand = 10;
            player.Vitals.Health.Kill();
            yield return null;
            Assert.AreEqual("results", GameSession.Instance.UI.OpenPanel);
            Assert.AreEqual(5, GameSession.Instance.Profile.starSand, "death keeps 50% materials");
            Assert.AreEqual(1, GameSession.Instance.Profile.deaths);
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            yield return WaitForScene("Workshop");
            Assert.IsFalse(player.Vitals.Health.IsDead, "revived in the workshop");
        }

        [UnityTest]
        public IEnumerator AmbientSpawnsRespectDistanceAndWalkableCells()
        {
            yield return Load("Forest");
            var director = RunManager.Current.Director;
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 10f));
            director.Elapsed = 300f; // mid-run pacing
            var spawner = EnemySpawner.Instance;
            var bad = new List<string>();
            spawner.Spawned += e =>
            {
                if (e.Def.isElite) return;
                float d = Vector2.Distance(e.Position, P);
                if (d < EnemySpawner.MinSpawnDistance - 0.01f && !director.WaveActive) bad.Add("too close " + d);
                if (!spawner.Grid.IsSpawnable(e.Position) && !director.WaveActive) bad.Add("not spawnable " + e.Position);
            };
            yield return new WaitForSeconds(5f);
            Assert.That(spawner.TotalSpawned, Is.GreaterThan(3), "ambient spawns happened");
            CollectionAssert.IsEmpty(bad);
        }
    }
}
