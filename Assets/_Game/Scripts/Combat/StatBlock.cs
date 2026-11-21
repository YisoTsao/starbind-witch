using System;
using System.Collections.Generic;

namespace Starbind.Combat
{
    public enum StatId
    {
        MaxHp, MaxMp, MpRegen, MoveSpeed, Damage, ReactionDamage, ResonanceGain,
        IceDuration, BurnDuration, ChainBonus, PullRadius, CooldownRate, ManaOnHit, ChilledDamage
    }

    /// <summary>Base values plus removable modifiers (GDD §9.2 StatModifier). Final =
    /// (base + sum(add)) * product(1 + mul). Removing a source restores the previous value exactly.</summary>
    public sealed class StatBlock
    {
        struct Mod { public StatId Stat; public float Add; public float Mul; public string Source; }

        readonly Dictionary<StatId, float> baseValues = new Dictionary<StatId, float>();
        readonly List<Mod> mods = new List<Mod>();
        public event Action Changed;

        public void SetBase(StatId id, float value)
        {
            baseValues[id] = value;
            Changed?.Invoke();
        }

        public float Base(StatId id) => baseValues.TryGetValue(id, out var v) ? v : 0f;

        public void AddModifier(string source, StatId stat, float add = 0f, float mul = 0f)
        {
            mods.Add(new Mod { Stat = stat, Add = add, Mul = mul, Source = source });
            Changed?.Invoke();
        }

        public int RemoveSource(string source)
        {
            int n = mods.RemoveAll(m => m.Source == source);
            if (n > 0)
                Changed?.Invoke();
            return n;
        }

        public float Get(StatId id)
        {
            float add = 0, mul = 1;
            foreach (var m in mods)
                if (m.Stat == id)
                {
                    add += m.Add;
                    mul *= 1f + m.Mul;
                }
            return (Base(id) + add) * mul;
        }
    }
}
