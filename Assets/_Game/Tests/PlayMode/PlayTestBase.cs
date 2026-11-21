using System.Collections;
using System.IO;
using NUnit.Framework;
using Starbind.Core;
using Starbind.Player;
using Starbind.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starbind.Tests
{
    /// <summary>Shared setup: saves go to a temp folder with a fresh profile, UI/pause state is reset,
    /// and the requested scene is loaded with device input off (tests drive the player directly).</summary>
    public abstract class PlayTestBase
    {
        protected PlayerController player;
        protected static string TempSaves => Path.Combine(Path.GetTempPath(), "starbind_playtests");

        protected IEnumerator Load(string scene, ProfileData profile = null)
        {
            for (float t = 0; Starbind.World.SceneTransition.IsBusy && t < 5f; t += Time.unscaledDeltaTime)
                yield return null; // never let a previous test's transition replace this scene
            var session = GameSession.Instance;
            session.RedirectSaves(TempSaves, profile ?? new ProfileData());
            session.ResetTransientState();
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            yield return null;
            player = PlayerController.Current;
            Assert.IsNotNull(player, "player in " + scene);
            player.InputEnabled = false;
        }

        [TearDown]
        public void ResetState()
        {
            if (GameSession.Exists)
                GameSession.Instance.ResetTransientState();
            Time.timeScale = 1f;
        }

        protected IEnumerator TeleportTo(Vector2 p)
        {
            player.Teleport(p);
            CameraRig.CameraFollow2D.Main?.SnapToTarget();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        protected IEnumerator Hold(Vector2 dir, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                player.Motor.SetMoveInput(dir);
                yield return null;
            }
            player.Motor.SetMoveInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
        }

        protected IEnumerator WaitForScene(string name)
        {
            yield return WaitUntilOrTimeout(() => SceneManager.GetActiveScene().name == name && PlayerController.Current != null && !Starbind.World.SceneTransition.IsBusy, 6f);
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            player = PlayerController.Current;
            player.InputEnabled = false;
        }

        protected static IEnumerator WaitUntilOrTimeout(System.Func<bool> cond, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond() && Time.realtimeSinceStartup < end)
                yield return null;
        }

        protected Vector2 P => player.transform.position;
    }
}
