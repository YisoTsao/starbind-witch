using System.Collections;
using System.Linq;
using NUnit.Framework;
using Starbind.Controls;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    /// <summary>Phone controls (mobile browser build) in chapter 1: the on-screen stick moves the witch,
    /// the HUD staff slot attacks, attacks auto-aim at the nearest enemy, and the layout switches back.</summary>
    public class TouchPlayTests : PlayTestBase
    {
        TouchControls touch;

        IEnumerator LoadWithTouch()
        {
            yield return Load("MoonveilForest");
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            player.InputEnabled = true;                     // this test drives the real input path (virtual pad)
            touch = GameSession.Instance.UI.Touch;
            Assert.IsNotNull(touch, "touch controls built with the UI");
            touch.SetActive(true);
            yield return null;
        }

        void TurnOff()
        {
            if (touch != null)
                touch.SetActive(false);
            if (player != null)
                player.InputEnabled = false;
            TouchControls.ForceOn = false;
        }

        static PointerEventData Pointer(Vector2 screen) =>
            new PointerEventData(EventSystem.current) { position = screen, pressPosition = screen, pointerId = 7 };

        static Vector2 ScreenCentre(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));

        [UnityTest]
        public IEnumerator StickMovesTheWitchAndLayoutSwitches()
        {
            yield return LoadWithTouch();
            try
            {
                var hud = GameSession.Instance.UI.Hud;
                Assert.IsTrue(hud.TouchLayout, "skill bar in thumb layout");
                Assert.IsTrue(PlayerInputReader.TouchMode);
                Assert.AreEqual(9, touch.Controls.Count, "stick + 7 skill slots + interact");
                Assert.That(touch.Controls.All(c => c.enabled));

                yield return TeleportTo(new Vector2(77.5f, 12f));
                var start = P;
                var stick = touch.Stick;
                var rt = (RectTransform)stick.transform;
                var c = ScreenCentre(rt);
                // drag the knob to the right edge of its range (in screen pixels)
                float scale = rt.lossyScale.x;
                var ev = Pointer(c);
                stick.OnPointerDown(ev);
                ev.position = c + new Vector2(TouchControls.StickRange * scale * 2f, 0);
                stick.OnDrag(ev);
                yield return new WaitForSeconds(0.6f);
                stick.OnPointerUp(ev);
                yield return new WaitForSeconds(0.15f);
                Assert.That(P.x - start.x, Is.GreaterThan(1.2f), $"witch walked east ({start} -> {P})");
                Assert.That(Mathf.Abs(P.y - start.y), Is.LessThan(0.6f));
                var still = P;
                yield return new WaitForSeconds(0.3f);
                Assert.That(Vector2.Distance(still, P), Is.LessThan(0.15f), "releasing the stick stops the witch");

                touch.SetActive(false);
                Assert.IsFalse(hud.TouchLayout, "keyboard layout restored");
                Assert.IsFalse(PlayerInputReader.TouchMode);
                Assert.That(touch.Controls.All(x => !x.enabled));
            }
            finally
            {
                TurnOff();
            }
        }

        [UnityTest]
        public IEnumerator StaffButtonAttacksTheNearestEnemy()
        {
            yield return LoadWithTouch();
            try
            {
                yield return TeleportTo(new Vector2(77.5f, 14f));
                var slime = EnemySpawner.Instance.SpawnAt("slime", P + new Vector2(-1.6f, 0.4f));
                slime.Health.InvulnerableUntil = 0;
                yield return null;
                // auto-aim: the aim point is the enemy, not the facing direction or a finger position
                var aim = player.Combat.AimPoint(player.Input);
                Assert.That(Vector2.Distance(aim, slime.Health.Center), Is.LessThan(0.01f), "auto-aim at the nearest enemy");

                var staff = touch.Controls.OfType<OnScreenButton>().First(b => b.controlPath == "<Gamepad>/rightTrigger");
                float hp = slime.Health.Current;
                var ev = Pointer(ScreenCentre((RectTransform)staff.transform));
                for (int i = 0; i < 3 && slime.Health.Current >= hp; i++)
                {
                    staff.OnPointerDown(ev);
                    yield return new WaitForSeconds(0.05f);
                    staff.OnPointerUp(ev);
                    yield return new WaitForSeconds(0.45f);
                }
                Assert.That(slime.Health.Current, Is.LessThan(hp), "staff tap hits the enemy to the west");
                Assert.That(player.Combat.AimDirection.x, Is.LessThan(-0.5f), "swung toward the enemy");
            }
            finally
            {
                TurnOff();
            }
        }
    }
}
