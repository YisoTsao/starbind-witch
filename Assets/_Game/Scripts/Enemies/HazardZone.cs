using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Lingering ground hazard (boss poison pool): damages the player each tick while inside.</summary>
    public sealed class HazardZone : MonoBehaviour
    {
        static ComponentPool<HazardZone> pool;
        static Transform root;
        readonly List<Health> inside = new List<Health>(2);

        float radius, endAt, nextTick, damage;
        SpriteFlipbook visual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool = null;
            root = null;
        }

        public static HazardZone Spawn(Vector2 p, float radius, float duration, float damagePerTick)
        {
            if (pool == null || root == null)
            {
                root = new GameObject("[Hazards]").transform;
                pool = new ComponentPool<HazardZone>(() =>
                {
                    var go = new GameObject("hazard");
                    go.transform.SetParent(root, false);
                    go.SetActive(false);
                    return go.AddComponent<HazardZone>();
                }, 24);
            }
            var z = pool.Get();
            if (z == null)
                return null;
            z.transform.position = p;
            z.radius = radius;
            z.endAt = Time.time + duration;
            z.nextTick = Time.time + 0.3f;
            z.damage = damagePerTick;
            z.visual = VfxService.Instance.Play("VFX_poison_pool", p, 0, radius, new Color(1, 1, 1, 0.85f), StarbindLayers.SortGroundDecal, 1, true);
            return z;
        }

        void Update()
        {
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.5f;
                var e = new DamageEvent { Amount = damage, Source = HitSource.Enemy, CannotTriggerReaction = true };
                CombatWorld.Instance.Query(transform.position, radius * 0.9f, Team.Enemy, inside, 2);
                foreach (var h in inside)
                    CombatWorld.Instance.Damage(e, h);
            }
            if (Time.time >= endAt)
            {
                VfxService.Instance.Release(visual);
                visual = null;
                pool.Release(this);
            }
        }
    }
}
