using System.Collections;
using NUnit.Framework;
using Starbind.CameraRig;
using Starbind.Player;
using Starbind.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Starbind.Tests
{
    /// <summary>Phase 0 acceptance in Play mode: spawn, movement, collision, camera, Y-sort fade,
    /// door interaction, keyboard bindings. Coordinates are tiles of the Phase-0 north block; the
    /// 48x40 village places that block at (10, 20), so every position goes through V().</summary>
    public class VillagePlayTests
    {
        PlayerController player;
        CameraFollow2D follow;
        Tilemap collision;

        [UnitySetUp]
        public IEnumerator LoadVillage()
        {
            SceneManager.LoadScene("Village");
            yield return null;
            yield return null;
            player = Object.FindAnyObjectByType<PlayerController>();
            follow = Object.FindAnyObjectByType<CameraFollow2D>();
            collision = GameObject.Find("Collision").GetComponent<Tilemap>();
            Assert.IsNotNull(player, "player");
            player.InputEnabled = false;
        }

        IEnumerator Hold(Vector2 dir, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(dir);
                yield return null;
            }
            player.Motor.SetMoveInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
        }

        const float OX = 10f, OY = 20f;
        static Vector2 V(float x, float y) => new Vector2(x + OX, y + OY);

        IEnumerator TeleportTo(float x, float y)
        {
            player.Teleport(V(x, y));
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        Vector2 P => player.transform.position;

        [UnityTest]
        public IEnumerator SpawnsAtSpawnSquare()
        {
            yield return null;
            var spawn = SpawnPoint.Find("spawn_square");
            Assert.IsNotNull(spawn);
            Assert.That(Vector2.Distance(P, spawn.transform.position), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator WalksAtDesignSpeed()
        {
            yield return TeleportTo(16.5f, 10.0f);
            float x0 = P.x;
            float t0 = Time.time;
            yield return Hold(Vector2.right, 1.0f);
            float speed = (P.x - x0) / (Time.time - t0);
            Assert.That(speed, Is.EqualTo(player.Motor.MoveSpeed).Within(0.07f * player.Motor.MoveSpeed), "units/s");
        }

        [UnityTest]
        public IEnumerator DiagonalIsNotFaster()
        {
            yield return TeleportTo(16.5f, 10.0f);
            var p0 = P;
            float t0 = Time.time;
            yield return Hold(new Vector2(1, -1), 0.5f);
            float speed = Vector2.Distance(P, p0) / (Time.time - t0);
            Assert.That(speed, Is.EqualTo(player.Motor.MoveSpeed).Within(0.07f * player.Motor.MoveSpeed));
        }

        [UnityTest]
        public IEnumerator RiverBlocksMovement()
        {
            yield return TeleportTo(7.5f, 5.5f);
            yield return Hold(Vector2.left, 1.5f); // 6.75 units of travel if nothing blocks
            Assert.That(P.x, Is.GreaterThan(6.0f + OX), "player entered the river");
            Assert.IsNull(collision.GetTile(collision.WorldToCell(P)), "standing on a blocked cell");
        }

        [UnityTest]
        public IEnumerator CliffFaceBlocksMovement()
        {
            yield return TeleportTo(17f, 10.3f);
            yield return Hold(Vector2.up, 1.0f);
            Assert.That(P.y, Is.LessThan(10.8f + OY), "walked into the south cliff face (y 11..12)");
        }

        [UnityTest]
        public IEnumerator CottageWallBlocksMovement()
        {
            yield return TeleportTo(12.5f, 16.3f);
            yield return Hold(Vector2.up, 1.0f);
            Assert.That(P.y, Is.LessThan(16.9f + OY), "walked through the cottage wall");
        }

        [UnityTest]
        public IEnumerator TreeTrunkBlocksMovement()
        {
            yield return TeleportTo(19.81f, 14.5f);
            yield return Hold(Vector2.up, 1.0f);
            Assert.That(P.y, Is.LessThan(14.95f + OY), "walked through the blossom tree trunk");
        }

        [UnityTest]
        public IEnumerator CameraStaysInsideMapBounds()
        {
            var cam = follow.GetComponent<Camera>();
            foreach (var corner in new[] { new Vector2(1.5f, 1.5f), new Vector2(46.5f, 38.5f) })
            {
                player.Teleport(corner); // absolute map corners
                yield return new WaitForFixedUpdate();
                yield return new WaitForSeconds(1.0f);
                var view = follow.ViewRect();
                var b = follow.Bounds;
                const float px = 1f / 32f;
                Assert.That(view.xMin, Is.GreaterThanOrEqualTo(b.xMin - px), $"left edge at {corner}");
                Assert.That(view.yMin, Is.GreaterThanOrEqualTo(b.yMin - px), $"bottom edge at {corner}");
                Assert.That(view.xMax, Is.LessThanOrEqualTo(b.xMax + px), $"right edge at {corner}");
                Assert.That(view.yMax, Is.LessThanOrEqualTo(b.yMax + px), $"top edge at {corner}");
                Assert.That(cam.orthographic, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator TreeFadesWhenPlayerIsBehindIt()
        {
            var tree = FindFaderNear(V(19.81f, 15.125f));
            yield return TeleportTo(19.81f, 16.0f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(tree.CurrentAlpha, Is.LessThan(0.6f), "behind the tree");
            yield return TeleportTo(19.81f, 14.6f);
            yield return new WaitForSeconds(0.5f);
            Assert.That(tree.CurrentAlpha, Is.GreaterThan(0.95f), "in front of the tree");
        }

        [UnityTest]
        public IEnumerator PlayerSortsOnWorldLayerByPivot()
        {
            yield return null;
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            var tree = FindFaderNear(V(19.81f, 15.125f)).GetComponent<SpriteRenderer>();
            Assert.AreEqual("World", sr.sortingLayerName);
            Assert.AreEqual(sr.sortingLayerName, tree.sortingLayerName);
            Assert.AreEqual(SpriteSortPoint.Pivot, sr.spriteSortPoint);
            Assert.AreEqual(SpriteSortPoint.Pivot, tree.spriteSortPoint);
        }

        [UnityTest]
        public IEnumerator CottageDoorShowsPromptAndInteracts()
        {
            yield return TeleportTo(14.53f, 16.4f);
            yield return null;
            var interactor = player.Interactor;
            Assert.IsNotNull(interactor.Current, "door not in reach");
            Assert.AreEqual("cottage_door", interactor.Current.InteractionId);
            var prompt = player.transform.Find("InteractPrompt").GetComponent<SpriteRenderer>();
            Assert.IsTrue(prompt.enabled, "F prompt hidden");
            var door = interactor.Current;
            Assert.IsTrue(interactor.TryInteract());
            Assert.AreEqual(1, door.InteractCount);
            yield return new WaitForSeconds(1.0f);
            StringAssert.Contains("Workshop", SceneTransition.LastMessage ?? "", "door did not request the Workshop scene");
            // the Workshop now exists: let the transition finish so it cannot replace the next test's scene
            for (float t = 0; SceneTransition.IsBusy && t < 5f; t += Time.unscaledDeltaTime)
                yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardDrivesMovementAndInteract()
        {
            // Batch-mode editors are never focused; by default the Input System drops unfocused input.
            var settings = InputSystem.settings;
            var prevBackground = settings.backgroundBehavior;
            var prevEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var kb = InputSystem.AddDevice<Keyboard>();
            try
            {
                player.InputEnabled = true;
                yield return TeleportTo(16.5f, 10.0f);
                float x0 = P.x;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D));
                yield return new WaitForSeconds(0.5f);
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return new WaitForFixedUpdate();
                Assert.That(P.x - x0, Is.GreaterThan(1.5f), "D key did not move the witch right");

                yield return TeleportTo(14.53f, 16.4f);
                yield return null;
                var door = player.Interactor.Current;
                Assert.IsNotNull(door);
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.F));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Assert.AreEqual(1, door.InteractCount, "F did not interact");
                for (float t = 0; SceneTransition.IsBusy && t < 5f; t += Time.unscaledDeltaTime)
                    yield return null;
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                settings.backgroundBehavior = prevBackground;
                settings.editorInputBehaviorInPlayMode = prevEditor;
            }
        }

        static OcclusionFader FindFaderNear(Vector2 p)
        {
            OcclusionFader best = null;
            float bestD = float.MaxValue;
            foreach (var f in Object.FindObjectsByType<OcclusionFader>())
            {
                float d = Vector2.Distance(f.transform.position, p);
                if (d < bestD) { bestD = d; best = f; }
            }
            Assert.That(bestD, Is.LessThan(0.05f), "no fader at " + p);
            return best;
        }
    }
}
