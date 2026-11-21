using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>Pooled 2D sound effects (GDD §12.4, draft synthesized clips). Same clip is rate-limited
    /// so a 60-enemy wave does not stack hundreds of identical sounds.</summary>
    public static class Sfx
    {
        const int Voices = 14;
        const float MinGap = 0.035f;
        static AudioSource[] sources;
        static int next;
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        public static float Volume = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources = null;
            next = 0;
            lastPlayed.Clear();
        }

        public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f)
        {
            var db = GameDatabase.Instance;
            var clip = db != null ? db.Sound(name) : null;
            if (clip == null)
                return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(name, out var t) && now - t < MinGap)
                return;
            lastPlayed[name] = now;
            Ensure();
            var src = sources[next];
            next = (next + 1) % Voices;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume * Volume);
        }

        static void Ensure()
        {
            if (sources != null && sources[0] != null)
                return;
            var go = new GameObject("[Sfx]");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].ignoreListenerPause = true;
            }
        }
    }
}
