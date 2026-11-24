using System.Collections.Generic;

namespace Starbind.Combat
{
    /// <summary>Decides which (at most one) primary reaction a hit triggers (GDD §5.5).
    /// Rules: reactions need an incoming element meeting a different existing mark; hits flagged
    /// CannotTriggerReaction never react; the consumed marks are removed; the same target cannot get
    /// the same reaction twice within ProtectionSeconds; a global budget caps reactions per second.
    /// Pure logic: no Unity objects, explicit time.</summary>
    public sealed class ReactionResolver
    {
        public const float ProtectionSeconds = 0.5f;
        public const int MaxTargetsPerReaction = 8;
        public int MaxReactionsPerSecond = 24;
        public int MaxProcDepth = 1;

        readonly Dictionary<long, float> lastReaction = new Dictionary<long, float>();
        readonly Queue<float> recent = new Queue<float>();
        readonly List<long> stale = new List<long>();

        public int TriggeredCount { get; private set; }
        public int BlockedByBudget { get; private set; }
        public int BlockedByProtection { get; private set; }

        static readonly ElementType[] Marked = { ElementType.Fire, ElementType.Ice, ElementType.Lightning, ElementType.Wind, ElementType.Nature, ElementType.Moon };

        public static ReactionType Pair(ElementType a, ElementType b)
        {
            if (a == b)
                return ReactionType.None;
            if (Is(a, b, ElementType.Fire, ElementType.Ice)) return ReactionType.SteamBurst;
            if (Is(a, b, ElementType.Fire, ElementType.Lightning)) return ReactionType.Overload;
            if (Is(a, b, ElementType.Ice, ElementType.Lightning)) return ReactionType.ShatterStorm;
            if (Is(a, b, ElementType.Fire, ElementType.Wind)) return ReactionType.FireTornado;
            if (Is(a, b, ElementType.Fire, ElementType.Nature)) return ReactionType.Wildfire;
            return ReactionType.None;
        }

        static bool Is(ElementType a, ElementType b, ElementType x, ElementType y) => (a == x && b == y) || (a == y && b == x);

        /// <summary>Call after the direct damage of <paramref name="hit"/> was applied. Returns the
        /// reaction (or None) and consumes the marks it used. Otherwise applies the hit's element as a
        /// new mark (when it has a status duration).</summary>
        public ReactionType Resolve(in DamageEvent hit, StatusSet status, float now)
        {
            status.Expire(now);
            var reaction = ReactionType.None;
            if (!hit.CannotTriggerReaction && hit.ProcDepth < MaxProcDepth && hit.Element != ElementType.None)
            {
                foreach (var existing in Marked)
                {
                    if (existing == hit.Element || !status.Has(existing, now))
                        continue;
                    var r = Pair(hit.Element, existing);
                    if (r == ReactionType.None)
                        continue;
                    if (!Allowed(hit.TargetEntityId, r, now))
                        break;
                    status.Consume(existing);
                    reaction = r;
                    break;
                }
            }
            if (reaction == ReactionType.None)
                status.Apply(hit.Element, hit.StatusDuration, now);
            else
            {
                status.Consume(hit.Element);
                TriggeredCount++;
            }
            return reaction;
        }

        bool Allowed(int target, ReactionType r, float now)
        {
            long key = ((long)target << 8) | (long)r;
            if (lastReaction.TryGetValue(key, out var t) && now - t < ProtectionSeconds)
            {
                BlockedByProtection++;
                return false;
            }
            while (recent.Count > 0 && now - recent.Peek() >= 1f)
                recent.Dequeue();
            if (recent.Count >= MaxReactionsPerSecond)
            {
                BlockedByBudget++;
                return false;
            }
            recent.Enqueue(now);
            lastReaction[key] = now;
            if (lastReaction.Count > 512)
                Prune(now);
            return true;
        }

        void Prune(float now)
        {
            stale.Clear();
            foreach (var kv in lastReaction)
                if (now - kv.Value > ProtectionSeconds)
                    stale.Add(kv.Key);
            foreach (var k in stale)
                lastReaction.Remove(k);
        }
    }
}
