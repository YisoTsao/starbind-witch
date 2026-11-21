using System.Collections.Generic;

namespace Starbind.Combat
{
    /// <summary>"Same hit event resolves once" (GDD §13.5, §14.2): remembers (castId, targetId) pairs.
    /// Pierce/orbit forms that may hit the same target again use a re-hit interval instead.</summary>
    public sealed class HitRegistry
    {
        readonly Dictionary<long, float> hits = new Dictionary<long, float>();
        readonly List<long> stale = new List<long>();
        public float Retention = 10f;

        static long Key(int castId, int targetId) => ((long)castId << 32) | (uint)targetId;

        /// <summary>True when this target was not hit by this cast yet (or the re-hit interval passed);
        /// records the hit.</summary>
        public bool TryRegister(int castId, int targetId, float now, float rehitInterval = float.PositiveInfinity)
        {
            long k = Key(castId, targetId);
            if (hits.TryGetValue(k, out var t) && now - t < rehitInterval)
                return false;
            hits[k] = now;
            if (hits.Count > 4096)
                Prune(now);
            return true;
        }

        public void Prune(float now)
        {
            stale.Clear();
            foreach (var kv in hits)
                if (now - kv.Value > Retention)
                    stale.Add(kv.Key);
            foreach (var k in stale)
                hits.Remove(k);
        }

        public int Count => hits.Count;
    }
}
