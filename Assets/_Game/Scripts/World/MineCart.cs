using System;
using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Mine cart on a straight rail (GDD §2.2 礦車爆破): F sends it rolling to its stop, where it
    /// blows up — rubble gate opens and nearby enemies take blast damage.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class MineCart : MonoBehaviour
    {
        public const float Speed = 4.5f;
        public const float BlastRadius = 2.6f;
        public const float BlastDamage = 60f;
        [SerializeField] Transform cart;
        [SerializeField] Vector2 stop;
        [SerializeField] string gate;
        bool rolling;

        public bool Exploded { get; private set; }

        public void Configure(Transform cartVisual, Vector2 stopAt, string gateId)
        {
            cart = cartVisual;
            stop = stopAt;
            gate = gateId;
        }

        void OnEnable() => GetComponent<Interactable>().Interacted += Push;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Push;

        public void Push(Interactable _)
        {
            if (rolling || Exploded)
                return;
            rolling = true;
            GameSession.Instance.UI.Notice("礦車出發！離遠一點");
            Sfx.Play("sfx_charge_enemy", 0.7f);
            GetComponent<Collider2D>().enabled = false;
        }

        void Update()
        {
            if (!rolling || cart == null)
                return;
            cart.position = Vector2.MoveTowards(cart.position, stop, Speed * Time.deltaTime);
            if (((Vector2)cart.position - stop).sqrMagnitude < 0.0004f)
                Blast();
        }

        public void Blast()
        {
            if (Exploded)
                return;
            rolling = false;
            Exploded = true;
            var v = VfxService.Instance;
            v.Play("VFX_blast", stop, 0, 2.6f);
            v.Play("VFX_shockwave", stop, 0, BlastRadius, new Color(1f, 0.7f, 0.4f), StarbindLayers.SortGroundDecal, 5);
            v.Sparks(stop, new Color(1f, 0.75f, 0.4f), 30, 6f);
            v.ScreenFlash(new Color(1f, 0.85f, 0.6f), 0.15f, 0.2f);
            CameraRig.CameraFollow2D.Main?.Shake(0.25f, 0.45f);
            Sfx.Play("sfx_overload", 1f);
            var e = new DamageEvent { CastId = CombatIds.NewCast(), Amount = BlastDamage, Source = HitSource.Environment, Knockback = 3f, CannotTriggerReaction = true };
            CombatWorld.Instance.DamageArea(stop, BlastRadius, e, Team.Player);
            if (cart != null)
                cart.gameObject.SetActive(false);
            PowerGate.Open(gate);
        }
    }
}
