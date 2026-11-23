using UnityEngine;

namespace Starbind.UI
{
    /// <summary>Entrance animation for modal UI (runs on unscaled time, so it plays while the game is paused):
    /// scale 0.8 → 1.04 → 1 and fade in, after an optional delay.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIPop : MonoBehaviour
    {
        public const float Duration = 0.2f;
        float start;
        CanvasGroup group;

        public static UIPop Add(GameObject go, float delay)
        {
            var p = go.AddComponent<UIPop>();
            p.start = Time.unscaledTime + delay;
            p.group = go.GetComponent<CanvasGroup>();
            p.Apply(0f);
            return p;
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / Duration);
            Apply(t);
            if (t >= 1f)
                enabled = false;
        }

        void Apply(float t)
        {
            float s = t < 0.7f ? Mathf.Lerp(0.8f, 1.04f, t / 0.7f) : Mathf.Lerp(1.04f, 1f, (t - 0.7f) / 0.3f);
            transform.localScale = new Vector3(s, s, 1f);
            if (group != null)
                group.alpha = t;
        }
    }
}
