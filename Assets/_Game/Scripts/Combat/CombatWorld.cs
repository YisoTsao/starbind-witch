using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Combat
{
    /// <summary>Scene-scoped combat hub: the registry of live Health components, the HitResolver
    /// (spatial queries + per-cast dedupe) and the DamageResolver (damage -> status -> bounded reaction
    /// -> events). Active spells, auto spells, reactions and enemies all go through Damage(); nothing
    /// else changes HP. VFX/UI listen to Bus and never decide damage.</summary>
    public sealed class CombatWorld : MonoBehaviour
    {
        static readonly List<Health> registry = new List<Health>(256);
        static CombatWorld instance;

        public const float BurnTick = 0.5f;
        public const float BurnDamage = 3f;
        public const float ShockDamageTaken = 1.1f;
        public const float PoisonDamage = 2.5f;
        public const float CurseDamageTaken = 1.15f;
        public const float WildfireRadius = 2.2f;
        public const float WildfireDamage = 12f;
        public const float WildfireBurnSeconds = 3f;

        public readonly CombatEventBus Bus = new CombatEventBus();
        public readonly HitRegistry Hits = new HitRegistry();
        public readonly ReactionResolver Reactions = new ReactionResolver();
        readonly List<Health> scratch = new List<Health>(64);
        readonly List<Health> chainHit = new List<Health>(8);
        float nextBurnTick;

        /// <summary>Multiplier for reaction damage (player stats + overdrive), set by the player each frame.</summary>
        public float ReactionDamageMultiplier { get; set; } = 1f;
        public int ChainBonus { get; set; }
        /// <summary>Extra damage fraction player hits deal to chilled targets (frost brooch), set by the player.</summary>
        public float ChilledBonus { get; set; }
        public int ActiveTornadoes { get; set; }
        public const int MaxTornadoes = 3;
        public System.Action<Vector2, Health> SpawnTornado; // set by the spell layer (FireTornadoZone pool)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registry.Clear();
            instance = null;
        }

        public static CombatWorld Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<CombatWorld>();
                    if (instance == null)
                        instance = new GameObject("[CombatWorld]").AddComponent<CombatWorld>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;
        public static IReadOnlyList<Health> All => registry;

        internal static void Register(Health h)
        {
            if (!registry.Contains(h))
                registry.Add(h);
        }

        internal static void Unregister(Health h) => registry.Remove(h);

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public static bool IsHostile(Team attacker, Team target) =>
            attacker == Team.Player ? target != Team.Player : attacker == Team.Enemy && target == Team.Player;

        // ------------------------------------------------------------------ HitResolver: queries
        /// <summary>Hostile, living targets whose body circle overlaps the query circle. Non-alloc.</summary>
        public int Query(Vector2 center, float radius, Team attacker, List<Health> results, int max = 64)
        {
            results.Clear();
            for (int i = 0; i < registry.Count && results.Count < max; i++)
            {
                var h = registry[i];
                if (h.IsDead || !IsHostile(attacker, h.Team))
                    continue;
                float r = radius + h.Radius;
                if ((h.Center - center).sqrMagnitude <= r * r)
                    results.Add(h);
            }
            return results.Count;
        }

        /// <summary>Targets inside a cone (staff swings).</summary>
        public int QueryArc(Vector2 center, Vector2 dir, float radius, float halfAngleDeg, Team attacker, List<Health> results)
        {
            Query(center, radius, attacker, results);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                var to = results[i].Center - center;
                if (to.sqrMagnitude > 0.04f && Vector2.Angle(dir, to) > halfAngleDeg)
                    results.RemoveAt(i);
            }
            return results.Count;
        }

        public Health Nearest(Vector2 p, float range, Team attacker, Health exclude = null, List<Health> excludeList = null, bool enemiesOnly = false)
        {
            Health best = null;
            float bestD = range * range;
            for (int i = 0; i < registry.Count; i++)
            {
                var h = registry[i];
                if (h.IsDead || h == exclude || !h.AutoTargetable || !IsHostile(attacker, h.Team) || (enemiesOnly && h.Team != Team.Enemy))
                    continue;
                if (excludeList != null && excludeList.Contains(h))
                    continue;
                float d = (h.Center - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            return best;
        }

        public int CountAlive(Team team)
        {
            int n = 0;
            foreach (var h in registry)
                if (!h.IsDead && h.Team == team)
                    n++;
            return n;
        }

        // ------------------------------------------------------------------ DamageResolver
        /// <summary>Applies one hit. CastId != 0 hits are deduplicated per target (rehitInterval lets
        /// orbit/zone forms hit the same target again after a delay).</summary>
        public DamageResult Damage(DamageEvent e, Health target, float rehitInterval = float.PositiveInfinity)
        {
            var result = new DamageResult();
            if (target == null || target.IsDead || !target.isActiveAndEnabled)
                return result;
            float now = Time.time;
            if (now < target.InvulnerableUntil)
                return result;
            if (e.CastId != 0 && !Hits.TryRegister(e.CastId, target.EntityId, now, rehitInterval))
                return result;
            e.TargetEntityId = target.EntityId;

            float amount = e.Amount * target.DamageTakenMultiplier;
            if (target.IncomingScale != null)
                amount *= target.IncomingScale(e);
            if (target.Status.Has(ElementType.Lightning, now))
                amount *= ShockDamageTaken;
            if (target.Status.Has(ElementType.Moon, now))
                amount *= CurseDamageTaken;
            if (ChilledBonus > 0 && target.Team != Team.Player && e.Source != HitSource.Environment && target.Status.Has(ElementType.Ice, now))
                amount *= 1f + ChilledBonus;
            amount = Mathf.Max(0, amount);
            e.Amount = amount;

            result.Applied = true;
            result.Dealt = target.Take(amount, now);
            result.Killed = target.IsDead;

            if (e.Knockback > 0 && target.TryGetComponent(out IKnockbackReceiver kb))
                kb.Knockback(e.Direction, e.Knockback);

            if (target.Team != Team.Player && e.Element != ElementType.None)
            {
                bool had = target.Status.Has(e.Element, now);
                result.Reaction = Reactions.Resolve(e, target.Status, now);
                if (result.Reaction == ReactionType.None && !had && target.Status.Has(e.Element, now))
                    Bus.RaiseElement(e.Element, target);
            }

            Bus.RaiseHit(target, e, result);
            target.RaiseDamaged(e, result);
            if (result.Reaction != ReactionType.None)
                TriggerReaction(result.Reaction, target, e);
            if (result.Killed)
            {
                Bus.RaiseKilled(target, e);
                target.RaiseDied();
            }
            return result;
        }

        /// <summary>Damages hostile targets whose body circle touches the segment (beams), nearest first,
        /// at most <paramref name="maxTargets"/>. Returns hits applied.</summary>
        public int DamageLine(Vector2 from, Vector2 dir, float length, float halfWidth, DamageEvent template, Team attacker, int maxTargets)
        {
            scratch.Clear();
            for (int i = 0; i < registry.Count; i++)
            {
                var h = registry[i];
                if (h.IsDead || !IsHostile(attacker, h.Team))
                    continue;
                var to = h.Center - from;
                float t = Mathf.Clamp(Vector2.Dot(to, dir), 0, length);
                float r = halfWidth + h.Radius;
                if ((from + dir * t - h.Center).sqrMagnitude <= r * r)
                    scratch.Add(h);
            }
            scratch.Sort((a, b) => (a.Center - from).sqrMagnitude.CompareTo((b.Center - from).sqrMagnitude));
            int n = 0;
            foreach (var h in scratch.ToArray())
            {
                if (n >= maxTargets)
                    break;
                var e = template;
                e.Direction = dir;
                if (Damage(e, h).Applied)
                    n++;
            }
            return n;
        }

        /// <summary>Damages every hostile target in a circle once (shared CastId). Returns hits applied.</summary>
        public int DamageArea(Vector2 center, float radius, DamageEvent template, Team attacker, int maxTargets = 64)
        {
            Query(center, radius, attacker, scratch, maxTargets);
            int n = 0;
            for (int i = 0; i < scratch.Count; i++)
            {
                var e = template;
                var dir = scratch[i].Center - center;
                e.Direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
                if (Damage(e, scratch[i]).Applied)
                    n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ reactions (bounded)
        public static DamageEvent ReactionEvent(in DamageEvent cause, float amount)
        {
            return new DamageEvent
            {
                CastId = CombatIds.NewCast(),
                SourceEntityId = cause.SourceEntityId,
                Amount = amount,
                Element = ElementType.None,
                CannotTriggerReaction = true,
                Source = HitSource.Reaction,
                ProcDepth = cause.ProcDepth + 1,
            };
        }

        void TriggerReaction(ReactionType r, Health target, in DamageEvent cause)
        {
            var p = target.Center;
            float mult = ReactionDamageMultiplier;
            Bus.RaiseReaction(r, p, target);
            switch (r)
            {
                case ReactionType.SteamBurst:
                    DamageArea(p, 1.8f, ReactionEvent(cause, 20f * mult), Team.Player, ReactionResolver.MaxTargetsPerReaction);
                    break;
                case ReactionType.Overload:
                {
                    var e = ReactionEvent(cause, 26f * mult);
                    e.Knockback = 3f;
                    DamageArea(p, 2.2f, e, Team.Player, ReactionResolver.MaxTargetsPerReaction);
                    break;
                }
                case ReactionType.ShatterStorm:
                {
                    int jumps = Mathf.Min(3 + ChainBonus, ReactionResolver.MaxTargetsPerReaction);
                    var e = ReactionEvent(cause, 16f * mult);
                    chainHit.Clear();
                    chainHit.Add(target);
                    var from = target;
                    Damage(e, target);
                    for (int i = 0; i < jumps; i++)
                    {
                        var next = Nearest(from.Center, 3.5f, Team.Player, null, chainHit);
                        if (next == null)
                            break;
                        chainHit.Add(next);
                        Bus.RaiseArc(from.Center, next.Center);
                        e.Direction = (next.Center - from.Center).normalized;
                        Damage(e, next);
                        from = next;
                    }
                    break;
                }
                case ReactionType.FireTornado:
                    if (ActiveTornadoes < MaxTornadoes && SpawnTornado != null)
                        SpawnTornado(p, target);
                    break;
                case ReactionType.Wildfire:
                {
                    // spreads burning to nearby enemies (marks only, no chain: CannotTriggerReaction)
                    Query(p, WildfireRadius, Team.Player, scratch, ReactionResolver.MaxTargetsPerReaction);
                    var e = ReactionEvent(cause, WildfireDamage * mult);
                    foreach (var h in scratch.ToArray())
                    {
                        Damage(e, h);
                        if (!h.IsDead)
                            h.Status.Apply(ElementType.Fire, WildfireBurnSeconds, Time.time);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ status ticking
        void Update()
        {
            float now = Time.time;
            if (now < nextBurnTick)
                return;
            nextBurnTick = now + BurnTick;
            scratch.Clear();
            foreach (var h in registry)
                if (!h.IsDead && h.Team != Team.Player && (h.Status.Has(ElementType.Fire, now) || h.Status.Has(ElementType.Nature, now)))
                    scratch.Add(h);
            foreach (var h in scratch)
            {
                float dot = (h.Status.Has(ElementType.Fire, now) ? BurnDamage : 0f) + (h.Status.Has(ElementType.Nature, now) ? PoisonDamage : 0f);
                var e = new DamageEvent
                {
                    Amount = dot,
                    Element = ElementType.None,
                    CannotTriggerReaction = true,
                    Source = HitSource.Environment,
                };
                Damage(e, h);
            }
            Hits.Prune(now);
        }
    }

    public interface IKnockbackReceiver
    {
        void Knockback(Vector2 direction, float strength);
        /// <summary>Continuous displacement request this frame (wind pull), units per second.</summary>
        void Pull(Vector2 velocity);
    }
}
