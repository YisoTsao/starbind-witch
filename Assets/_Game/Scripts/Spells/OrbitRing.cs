using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>frost_orbit (GDD §5.3): ice blades circling the caster. Each blade hit uses the ring's
    /// CastId with a per-target re-hit interval (the "internal CD per enemy" of §5.4).</summary>
    public sealed class OrbitRing : MonoBehaviour
    {
        public const float RehitInterval = 0.6f;
        public float Radius = 1.35f;
        public float DegreesPerSecond = 220f;

        readonly List<SpriteRenderer> blades = new List<SpriteRenderer>();
        readonly List<Health> hits = new List<Health>(8);
        DamageEvent template;
        int castId;
        float angle;
        float nextFrostTrail;

        public int BladeCount => blades.Count;
        public RuntimeSpell Spell { get; private set; }

        public void Configure(RuntimeSpell spell, int sourceEntity, float damageMultiplier, float statusDuration)
        {
            Spell = spell;
            if (castId == 0)
                castId = CombatIds.NewCast();
            template = new DamageEvent
            {
                CastId = castId,
                SourceEntityId = sourceEntity,
                Amount = spell.Damage * damageMultiplier,
                Element = ElementType.Ice,
                StatusDuration = statusDuration,
                Source = HitSource.AutoSpell,
                Knockback = 0.6f,
            };
            while (blades.Count < spell.Count)
                blades.Add(CreateBlade());
            for (int i = 0; i < blades.Count; i++)
                blades[i].gameObject.SetActive(i < spell.Count);
        }

        SpriteRenderer CreateBlade()
        {
            var go = new GameObject("frost_blade");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            var db = GameDatabase.Instance;
            sr.sprite = db != null ? db.Sprite("VFX_frost_blade") : null;
            go.transform.localScale = Vector3.one * 0.68f;
            if (db != null && db.unlitMaterial != null)
                sr.sharedMaterial = db.unlitMaterial;
            sr.sortingLayerName = StarbindLayers.SortVfx;
            sr.sortingOrder = 6;
            return sr;
        }

        void Update()
        {
            if (Spell == null)
                return;
            angle += DegreesPerSecond * Time.deltaTime;
            int n = 0;
            foreach (var b in blades)
                if (b.gameObject.activeSelf)
                    n++;
            if (n == 0)
                return;
            var world = CombatWorld.Instance;
            bool leaveTrail = Time.time >= nextFrostTrail;
            if (leaveTrail)
                nextFrostTrail = Time.time + 0.055f;
            int i = 0;
            foreach (var b in blades)
            {
                if (!b.gameObject.activeSelf)
                    continue;
                float a = (angle + i * 360f / n) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * Radius + new Vector2(0, 0.4f);
                b.transform.localPosition = offset;
                var tangent = new Vector2(-Mathf.Sin(a), 0.8f * Mathf.Cos(a));
                var facing = Mathf.Round(Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg / 22.5f) * 22.5f;
                b.transform.localRotation = Quaternion.Euler(0, 0, facing);
                bool front = offset.y < 0.4f;
                b.sortingLayerName = front ? StarbindLayers.SortVfx : StarbindLayers.SortGroundDecal;
                b.color = front ? Color.white : new Color(0.65f, 0.8f, 0.9f, 0.82f);
                Vector2 p = b.transform.position;
                if (leaveTrail)
                    VfxService.Instance.Embers(p, new Color(0.68f, 0.9f, 1f), -tangent * 1.1f, 1, 0.27f, 0.075f);
                world.Query(p, 0.28f, Team.Player, hits, 4);
                foreach (var h in hits)
                {
                    var e = template;
                    e.Direction = (h.Center - (Vector2)transform.position).normalized;
                    if (world.Damage(e, h, RehitInterval).Applied)
                    {
                        SpellFx.Impact(ElementType.Ice, h.Center, 0.42f);
                        Sfx.Play("sfx_ice_hit", 0.35f);
                    }
                }
                i++;
            }
        }
    }
}
