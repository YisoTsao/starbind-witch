using System;
using UnityEngine;

namespace Starbind.Combat
{
    /// <summary>Scene-scoped combat events (GDD §13.6). Owned by CombatWorld, never static, so a scene
    /// change drops every subscriber; subscribers still unsubscribe in OnDisable.</summary>
    public sealed class CombatEventBus
    {
        public event Action<string, CastContext> SpellCast;
        public event Action<Health, DamageEvent, DamageResult> Hit;
        public event Action<ReactionType, Vector2, Health> ReactionTriggered;
        public event Action<Health, DamageEvent> Killed;
        public event Action<ElementType, Health> ElementApplied;
        public event Action<Vector2, Vector2> Arc;

        public void RaiseSpellCast(string id, in CastContext ctx) => SpellCast?.Invoke(id, ctx);
        public void RaiseHit(Health h, in DamageEvent e, in DamageResult r) => Hit?.Invoke(h, e, r);
        public void RaiseReaction(ReactionType r, Vector2 p, Health h) => ReactionTriggered?.Invoke(r, p, h);
        public void RaiseKilled(Health h, in DamageEvent e) => Killed?.Invoke(h, e);
        public void RaiseElement(ElementType el, Health h) => ElementApplied?.Invoke(el, h);
        public void RaiseArc(Vector2 a, Vector2 b) => Arc?.Invoke(a, b);
    }
}
