using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>Fire Tornado reaction (Fire + Wind, GDD §5.5): a 3 s moving fire zone. Its ticks are
    /// CannotTriggerReaction secondary damage; at most CombatWorld.MaxTornadoes exist at once.</summary>
    public sealed class FireTornadoZone : MonoBehaviour
    {
        public const float Duration = 3f;
        public const float Radius = 1.3f;
        public const float TickDamage = 7f;

        float endAt;
        float nextTick;
        SpriteFlipbook flip;
        SpriteFlipbook bloom;
        float nextEmber;
        bool live;

        public void Launch(Vector2 p)
        {
            transform.position = p;
            endAt = Time.time + Duration;
            nextTick = Time.time;
            nextEmber = Time.time;
            flip = flip != null ? flip : GetComponent<SpriteFlipbook>();
            flip.Renderer.sortingOrder = 8;
            flip.Renderer.color = Color.white;
            flip.Play(GameDatabase.Instance.Sprites("VFX_fire_tornado"), true);
            transform.localScale = Vector3.one;
            bloom = VfxService.Instance.Glow(p + Vector2.up * 0.55f, new Color(1f, 0.42f, 0.12f, 0.59f),
                3.8f, target: transform);
            live = true;
            CombatWorld.Instance.ActiveTornadoes++;
            Sfx.Play("sfx_tornado", 0.9f);
        }

        void Update()
        {
            if (!live)
                return;
            float now = Time.time;
            var world = CombatWorld.Instance;
            var target = world.Nearest(transform.position, 6f, Team.Player, null, null, true);
            if (target != null)
                transform.position = Vector2.MoveTowards(transform.position, target.Center - new Vector2(0, 0.3f), 1.6f * Time.deltaTime);
            if (now >= nextEmber)
            {
                nextEmber = now + 0.05f;
                VfxService.Instance.Embers((Vector2)transform.position + new Vector2(Random.Range(-0.4f, 0.4f), 0.2f), new Color(1f, 0.55f, 0.2f), new Vector2(0, 3.5f), 1, 0.5f, 0.11f);
            }
            if (now >= nextTick)
            {
                nextTick = now + 0.4f;
                var e = new DamageEvent
                {
                    CastId = CombatIds.NewCast(),
                    Amount = TickDamage * world.ReactionDamageMultiplier,
                    Element = ElementType.Fire,
                    StatusDuration = 2f,
                    CannotTriggerReaction = true,
                    Source = HitSource.Reaction,
                    ProcDepth = 1,
                };
                world.DamageArea((Vector2)transform.position + new Vector2(0, 0.3f), Radius, e, Team.Player, ReactionResolver.MaxTargetsPerReaction);
                VfxService.Instance.Sparks((Vector2)transform.position + new Vector2(0, 0.6f), new Color(1f, 0.6f, 0.2f), 4, 2.5f);
                VfxService.Instance.Light(transform.position, new Color(1f, 0.55f, 0.2f), 1.9f, 0.55f, 0.4f);
            }
            if (now >= endAt)
            {
                live = false;
                world.ActiveTornadoes = Mathf.Max(0, world.ActiveTornadoes - 1);
                SpellPools.Instance.Tornadoes.Release(this);
            }
        }

        void OnDisable()
        {
            if (bloom != null)
            {
                VfxService.Instance.Release(bloom);
                bloom = null;
            }
            if (live && CombatWorld.Exists)
                CombatWorld.Instance.ActiveTornadoes = Mathf.Max(0, CombatWorld.Instance.ActiveTornadoes - 1);
            live = false;
        }
    }
}
