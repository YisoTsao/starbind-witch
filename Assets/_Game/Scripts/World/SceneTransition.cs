using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Starbind.World
{
    /// <summary>Fade out, load a scene, place the player at a named spawn, fade in.
    /// If the target scene is not in the build yet, it fades and reports instead of failing.</summary>
    public static class SceneTransition
    {
        static string pendingSpawnId;
        static Runner runner;

        public static bool IsBusy => runner != null && runner.Busy;
        public static string LastMessage { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pendingSpawnId = null;
            runner = null;
            LastMessage = null;
        }

        public static string ConsumePendingSpawnId()
        {
            var id = pendingSpawnId;
            pendingSpawnId = null;
            return id;
        }

        public static void Go(string sceneName, string spawnId)
        {
            if (IsBusy)
                return;
            if (runner == null)
            {
                var go = new GameObject("[SceneTransition]");
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            runner.StartCoroutine(runner.Run(sceneName, spawnId));
        }

        sealed class Runner : MonoBehaviour
        {
            const float FadeTime = 0.25f;
            Image fade;
            public bool Busy { get; private set; }

            void Awake()
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                var img = new GameObject("Fade").AddComponent<Image>();
                img.transform.SetParent(transform, false);
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
                img.color = new Color(0, 0, 0, 0);
                img.raycastTarget = false;
                fade = img;
            }

            public IEnumerator Run(string sceneName, string spawnId)
            {
                Busy = true;
                yield return Fade(0, 1);
                if (Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    pendingSpawnId = spawnId;
                    LastMessage = "loaded " + sceneName;
                    yield return SceneManager.LoadSceneAsync(sceneName);
                }
                else
                {
                    LastMessage = "scene '" + sceneName + "' is not built yet (next phase)";
                    Debug.Log("[SceneTransition] " + LastMessage);
                    yield return new WaitForSeconds(0.2f);
                }
                yield return Fade(1, 0);
                Busy = false;
            }

            IEnumerator Fade(float from, float to)
            {
                for (float t = 0; t < FadeTime; t += Time.unscaledDeltaTime)
                {
                    fade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / FadeTime));
                    yield return null;
                }
                fade.color = new Color(0, 0, 0, to);
            }
        }
    }
}
