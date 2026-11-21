using System.Collections.Generic;
using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Single owner of Time.timeScale. Menus push a reason (timeScale 0); hit-stop briefly
    /// slows time only when nothing is paused. Every gameplay timer uses scaled time, so cooldowns and
    /// buffs stay consistent across pauses (GDD §14.3).</summary>
    public static class GamePause
    {
        static readonly HashSet<string> reasons = new HashSet<string>();
        static float hitStopUntil;
        static Runner runner;

        public static bool IsPaused => reasons.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            reasons.Clear();
            hitStopUntil = 0;
            runner = null;
            Time.timeScale = 1f;
        }

        public static void Push(string reason)
        {
            reasons.Add(reason);
            Apply();
        }

        public static void Pop(string reason)
        {
            reasons.Remove(reason);
            Apply();
        }

        public static void Clear()
        {
            reasons.Clear();
            hitStopUntil = 0;
            Apply();
        }

        /// <summary>Freeze-frame on heavy hits (GDD §4.4 feedback). Real seconds.</summary>
        public static void HitStop(float seconds)
        {
            if (IsPaused)
                return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
            EnsureRunner();
            Apply();
        }

        static void Apply()
        {
            if (reasons.Count > 0)
                Time.timeScale = 0f;
            else if (Time.unscaledTime < hitStopUntil)
                Time.timeScale = 0.05f;
            else
                Time.timeScale = 1f;
        }

        static void EnsureRunner()
        {
            if (runner != null)
                return;
            var go = new GameObject("[GamePause]");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (hitStopUntil > 0 && Time.unscaledTime >= hitStopUntil)
                {
                    hitStopUntil = 0;
                    Apply();
                }
            }
        }
    }
}
