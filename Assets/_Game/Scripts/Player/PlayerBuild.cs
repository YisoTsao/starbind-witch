using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Progression;
using Starbind.Save;
using Starbind.Spells;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>The player's spell build for this scene/run (GDD §6.1): Q/E active slots and the auto
    /// slots, created from the profile loadout through the whitelisted SpellFactory. Implements the
    /// upgrade target interface.</summary>
    [RequireComponent(typeof(AbilityRunner), typeof(AutoCastController), typeof(PlayerVitals))]
    public sealed class PlayerBuild : MonoBehaviour, IBuildTarget
    {
        readonly RuntimeSpell[] active = new RuntimeSpell[2];
        AbilityRunner runner;
        AutoCastController auto;
        PlayerVitals vitals;
        int chainBonus;

        public RuntimeSpell Active(int slot) => slot >= 0 && slot < 2 ? active[slot] : null;
        public AutoCastController Auto => auto;
        public StatBlock Stats => vitals.Stats;
        public bool CanAddAuto => auto.CanAdd;

        public int ChainBonus
        {
            get => chainBonus;
            set
            {
                chainBonus = value;
                if (CombatWorld.Exists)
                    CombatWorld.Instance.ChainBonus = value;
            }
        }

        void Awake()
        {
            runner = GetComponent<AbilityRunner>();
            auto = GetComponent<AutoCastController>();
            vitals = GetComponent<PlayerVitals>();
        }

        public void LoadFrom(ProfileData p)
        {
            var db = GameDatabase.Instance;
            if (db == null)
                return;
            active[0] = Make(db.Spell(p.activeQ));
            active[1] = Make(db.Spell(p.activeE));
            auto.Clear();
            auto.UnlockedSlots = 2;
            foreach (var id in p.autoSpells)
                AddAuto(id);
            ChainBonus = 0;
        }

        static RuntimeSpell Make(SpellDefinition def) => def != null && SpellFactory.IsAllowed(def) ? SpellFactory.Create(def) : null;

        public void SetActive(int slot, string spellId)
        {
            var def = GameDatabase.Instance.Spell(spellId);
            active[slot] = Make(def);
        }

        public RuntimeSpell FindSpell(string id)
        {
            foreach (var s in active)
                if (s != null && s.Id == id)
                    return s;
            return auto.Get(id);
        }

        public bool AddAuto(string id)
        {
            var def = GameDatabase.Instance.Spell(id);
            if (def == null || def.trigger != SpellTrigger.Auto)
                return false;
            var s = Make(def);
            return s != null && auto.Add(s);
        }

        public void RefreshSpell(RuntimeSpell s) => auto.Refresh(s);

        public void Heal(float amount) => vitals.Health.Heal(amount);

        public IEnumerable<RuntimeSpell> AllSpells()
        {
            foreach (var s in active)
                if (s != null)
                    yield return s;
            foreach (var s in auto.Slots)
                yield return s;
        }
    }
}
