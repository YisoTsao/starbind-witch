#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using Starbind.Core;
using Starbind.Player;
using Starbind.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    public class ChapterWalkthroughTests : PlayTestBase
    {
        [UnityTest]
        public IEnumerator PlayerWalksIntoWallAndUsesAllFourReturnRoutes()
        {
            yield return Load("Village");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Game/Scenes/Dev/ChapterWalkthrough.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = PlayerController.Current;
            Assert.IsNotNull(player);
            player.InputEnabled = false;
            var origin = P;
            yield return Hold(Vector2.down, 1.3f);
            Assert.That(origin.y - P.y, Is.InRange(2f, 3.7f), "entrance room south wall blocks normal movement");

            var atlas = Object.FindAnyObjectByType<ChapterGraybox>().atlas;
            var layouts = JsonUtility.FromJson<ChapterAtlas>(atlas.text).chapters;
            var interact = player.GetComponent<PlayerInteractor>();
            foreach (var layout in layouts)
            {
                if (string.IsNullOrEmpty(layout.next)) continue;
                var exit = SpawnPoint.Find(layout.id + ".4");
                var arrival = SpawnPoint.Find(layout.next + ".0");
                yield return TeleportTo(exit.transform.position);
                Assert.IsTrue(interact.TryInteract());
                Assert.Less(Vector2.Distance(P, arrival.transform.position), .1f);
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(ZoneMap.Current.TryGet(P, out var zone));
                Assert.AreEqual(layout.next + ".0", zone.id);
                if (layout.next == "mine")
                {
                    var dir = Path.Combine(Application.dataPath, "../TestResults/graybox");
                    Directory.CreateDirectory(dir);
                    yield return null;
                    var cam = Camera.main;
                    var target = RenderTexture.GetTemporary(960, 540, 24);
                    var oldTarget = cam.targetTexture;
                    var oldActive = RenderTexture.active;
                    var frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        cam.targetTexture = target;
                        cam.Render();
                        RenderTexture.active = target;
                        frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        frame.Apply();
                        File.WriteAllBytes(Path.Combine(dir, "mine-play.png"), frame.EncodeToPNG());
                    }
                    finally
                    {
                        cam.targetTexture = oldTarget;
                        RenderTexture.active = oldActive;
                        RenderTexture.ReleaseTemporary(target);
                        Object.Destroy(frame);
                    }
                }
                Assert.IsTrue(interact.TryInteract());
                Assert.Less(Vector2.Distance(P, exit.transform.position), .1f);
            }
        }
    }
}
#endif
