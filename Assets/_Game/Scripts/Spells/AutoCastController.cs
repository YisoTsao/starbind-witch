using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>Auto spell slots (GDD §6.2): re-evaluates targets every QueryInterval, casts through the
    /// shared AbilityRunner only when the slot is ready AND a valid target is in range, respects a
    /// per-frame cast budget, and never fires at nothing. Orbit spells stay active instead.</summary>
    [RequireComponent(typeof(AbilityRunner))]
    public sealed class AutoCastController : MonoBehaviour
    {
        public const int MaxSlots = 4;
        public const float QueryInterval = 0.2f;
        public const int CastBudgetPerFrame = 2;

        readonly List<RuntimeSpell> slots = new List<RuntimeSpell>();
        AbilityRunner runner;
        float nextQuery;

        public bool Enabled { get; set; } = true;
        public int UnlockedSlots { get; set; } = 2;
        public IReadOnlyList<RuntimeSpell> Slots => slots;
        public int SkippedNoTarget { get; private set; }
        public int AutoCasts { get; private set; }

        void Awake() => runner = GetComponent<AbilityRunner>();

        public bool CanAdd => slots.Count < Mathf.Min(UnlockedSlots, MaxSlots);

        public bool Add(RuntimeSpell s)
        {
            if (s == null || !CanAdd || Has(s.Id))
                return false;
            slots.Add(s);
            if (s.Form == SpellForm.Orbit)
                runner.TryCast(s, transform.position);
            return true;
        }

        public bool Has(string id)
        {
            foreach (var s in slots)
                if (s.Id == id)
                    return true;
            return false;
        }

        public RuntimeSpell Get(string id)
        {
            foreach (var s in slots)
                if (s.Id == id)
                    return s;
            return null;
        }

        public void Clear()
        {
            slots.Clear();
            if (runner != null && runner.Orbit != null)
                runner.Orbit.gameObject.SetActive(false);
        }

        /// <summary>Re-applies an orbit spell after its blade count changed.</summary>
        public void Refresh(RuntimeSpell s)
        {
            if (s.Form == SpellForm.Orbit)
            {
                runner.Orbit.gameObject.SetActive(true);
                s.ReadyAt = 0;
                runner.TryCast(s, transform.position);
            }
        }

        void Update()
        {
            if (!Enabled || GamePause.IsPaused || Time.time < nextQuery)
                return;
            nextQuery = Time.time + QueryInterval;
            int budget = CastBudgetPerFrame;
            var world = CombatWorld.Instance;
            foreach (var s in slots)
            {
                if (s.Form == SpellForm.Orbit)
                {
                    if (!runner.Orbit.gameObject.activeSelf || runner.Orbit.Spell != s)
                        Refresh(s);
                    continue;
                }
                if (budget <= 0)
                    break;
                if (!runner.IsReady(s))
                    continue;
                var target = world.Nearest((Vector2)transform.position + new Vector2(0, 0.4f), s.Range, Team.Player);
                if (target == null)
                {
                    SkippedNoTarget++; // no cast, no cooldown, no particles (GDD §6.2)
                    continue;
                }
                if (runner.TryCast(s, target.Center, target) == CastResult.Cast)
                {
                    AutoCasts++;
                    budget--;
                }
            }
        }
    }
}
