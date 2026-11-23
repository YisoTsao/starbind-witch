using System;
using Starbind.Core;
using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>Plays a SpriteSet on a SpriteRenderer (scaled time). One-shot flipbooks raise
    /// Finished; looping ones run until stopped. Display only.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        SpriteRenderer sr;
        Sprite[] frames;
        float fps;
        float t;
        float startAlpha;
        int first, count;
        public bool Loop { get; set; }
        public bool Playing { get; private set; }
        public bool UseUnscaledTime { get; set; }
        public event Action<SpriteFlipbook> Finished;

        public SpriteRenderer Renderer => sr != null ? sr : sr = GetComponent<SpriteRenderer>();
        public int Frame { get; private set; }
        public Vector2 FollowOffset { get; set; }
        public bool FadeOut { get; set; }

        public void Play(SpriteSet set, bool loop, float fpsOverride = 0f, int firstFrame = 0, int frameCount = -1)
        {
            if (set == null || set.frames == null || set.frames.Length == 0)
            {
                Playing = false;
                return;
            }
            Play(set.frames, fpsOverride > 0 ? fpsOverride : set.fps, loop, firstFrame, frameCount);
        }

        public void Play(Sprite[] f, float framesPerSecond, bool loop, int firstFrame = 0, int frameCount = -1)
        {
            frames = f;
            fps = Mathf.Max(0.01f, framesPerSecond);
            Loop = loop;
            first = Mathf.Clamp(firstFrame, 0, f.Length - 1);
            count = frameCount < 0 ? f.Length - first : Mathf.Min(frameCount, f.Length - first);
            t = 0;
            startAlpha = Renderer.color.a;
            Playing = true;
            Frame = 0;
            Renderer.sprite = frames[first];
        }

        /// <summary>Changes the frame range without restarting time when the same range is requested.</summary>
        public void SetRange(int firstFrame, int frameCount, float framesPerSecond)
        {
            if (frames == null)
                return;
            if (firstFrame == first && frameCount == count)
            {
                fps = framesPerSecond;
                return;
            }
            first = Mathf.Clamp(firstFrame, 0, frames.Length - 1);
            count = Mathf.Max(1, Mathf.Min(frameCount, frames.Length - first));
            fps = framesPerSecond;
            t = 0;
            Frame = 0;
            Renderer.sprite = frames[first];
            Playing = true;
            Loop = true;
        }

        public void Stop() => Playing = false;

        void Update()
        {
            if (!Playing || frames == null)
                return;
            t += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (FadeOut && !Loop)
            {
                var color = Renderer.color;
                float left = 1f - Mathf.Clamp01(t * fps / count);
                color.a = startAlpha * left * left;
                Renderer.color = color;
            }
            int f = (int)(t * fps);
            if (f >= count)
            {
                if (Loop)
                {
                    f %= count;
                }
                else
                {
                    Playing = false;
                    Finished?.Invoke(this);
                    return;
                }
            }
            if (f != Frame)
            {
                Frame = f;
                Renderer.sprite = frames[first + f];
            }
        }
    }
}
