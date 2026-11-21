using System;
using System.Collections.Generic;

namespace Starbind.Core
{
    /// <summary>Seeded, platform-stable random source (xorshift128+). Every gameplay roll that must
    /// be reproducible (spawns, loot, upgrade cards) goes through one of these, never UnityEngine.Random.</summary>
    [Serializable]
    public sealed class GameRng
    {
        ulong s0, s1;

        public GameRng(int seed)
        {
            ulong x = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            s0 = SplitMix(ref x);
            s1 = SplitMix(ref x);
            if (s0 == 0 && s1 == 0)
                s1 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong a = s0, b = s1;
            s0 = b;
            a ^= a << 23;
            s1 = a ^ b ^ (a >> 17) ^ (b >> 26);
            return s1 + b;
        }

        /// <summary>[0, 1)</summary>
        public float Value() => (NextULong() >> 40) / (float)(1UL << 24);

        /// <summary>[min, max)</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextULong() % (ulong)(max - min));

        public float Range(float min, float max) => min + (max - min) * Value();

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

        /// <summary>Index chosen by weight; -1 when all weights are zero.</summary>
        public int Weighted(IReadOnlyList<float> weights)
        {
            float total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += Math.Max(0, weights[i]);
            if (total <= 0)
                return -1;
            float r = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r < 0)
                    return i;
            }
            return weights.Count - 1;
        }
    }
}
