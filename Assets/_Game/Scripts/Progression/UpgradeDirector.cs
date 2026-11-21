using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Spells;

namespace Starbind.Progression
{
    /// <summary>What an upgrade can change on the player's build (implemented by PlayerBuild; faked in tests).</summary>
    public interface IBuildTarget
    {
        StatBlock Stats { get; }
        RuntimeSpell FindSpell(string id);
        bool CanAddAuto { get; }
        bool AddAuto(string id);
        void RefreshSpell(RuntimeSpell s);
        void Heal(float amount);
        int ChainBonus { get; set; }
    }

    /// <summary>Level-up three-choice (GDD §8.1-8.2): weighted, seeded, filtered by prerequisites,
    /// exclusions, free auto slots and stack limits. Applies the 15 MVP card effects. Never writes the
    /// permanent profile.</summary>
    public sealed class UpgradeDirector
    {
        public const int MaxChainBonus = 5; // 3 base jumps + 5 = 8 = reaction target cap

        readonly List<UpgradeDefinition> pool;
        readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
        readonly List<UpgradeDefinition> eligible = new List<UpgradeDefinition>();
        readonly List<float> weights = new List<float>();

        public UpgradeDirector(IEnumerable<UpgradeDefinition> cards) => pool = new List<UpgradeDefinition>(cards);

        public IReadOnlyDictionary<string, int> Stacks => stacks;
        public int StackOf(string id) => stacks.TryGetValue(id, out var n) ? n : 0;

        static bool HasAny(IBuildTarget b, string ids)
        {
            foreach (var id in ids.Split('|'))
                if (b.FindSpell(id) != null)
                    return true;
            return false;
        }

        static readonly Dictionary<string, string> grantsAuto = new Dictionary<string, string>
        {
            { "U07", "frost_orbit" }, { "U08", "thunder_pulse" }, { "U09", "star_fire" },
        };

        public bool IsEligible(UpgradeDefinition u, IBuildTarget b)
        {
            if (u == null || StackOf(u.id) >= u.maxStacks)
                return false;
            if (!string.IsNullOrEmpty(u.requiresSpell) && !HasAny(b, u.requiresSpell))
                return false;
            if (!string.IsNullOrEmpty(u.excludesSpell) && HasAny(b, u.excludesSpell))
                return false;
            if (grantsAuto.TryGetValue(u.id, out var spell) && b.FindSpell(spell) == null && !b.CanAddAuto)
                return false;
            if (u.id == "U04" && b.ChainBonus >= MaxChainBonus)
                return false;
            return true;
        }

        /// <summary>Up to <paramref name="count"/> distinct eligible cards, weighted, from <paramref name="rng"/>.</summary>
        public List<UpgradeDefinition> Roll(GameRng rng, IBuildTarget b, int count = 3)
        {
            eligible.Clear();
            foreach (var u in pool)
                if (IsEligible(u, b))
                    eligible.Add(u);
            var result = new List<UpgradeDefinition>(count);
            while (result.Count < count && eligible.Count > 0)
            {
                weights.Clear();
                foreach (var u in eligible)
                    weights.Add(u.weight);
                int i = rng.Weighted(weights);
                if (i < 0)
                    break;
                result.Add(eligible[i]);
                eligible.RemoveAt(i);
            }
            return result;
        }

        public bool Apply(UpgradeDefinition u, IBuildTarget b)
        {
            if (!IsEligible(u, b))
                return false;
            stacks[u.id] = StackOf(u.id) + 1;
            string src = u.id + "#" + stacks[u.id];
            var s = b.Stats;
            switch (u.id)
            {
                case "U01": Mod(b, "fire_bolt", x => x.Split = 2); break;
                case "U02": Mod(b, "fire_bolt", x => x.Pierce += 1); break;
                case "U03": s.AddModifier(src, StatId.IceDuration, mul: 0.25f); break;
                case "U04": b.ChainBonus = System.Math.Min(MaxChainBonus, b.ChainBonus + 1); break;
                case "U05": s.AddModifier(src, StatId.PullRadius, mul: 0.2f); break;
                case "U06": Mod(b, "homing_missile", x => x.Count += 1); break;
                case "U07":
                    if (b.FindSpell("frost_orbit") != null)
                        Mod(b, "frost_orbit", x => x.Count += 1);
                    else
                        b.AddAuto("frost_orbit");
                    break;
                case "U08": b.AddAuto("thunder_pulse"); break;
                case "U09": b.AddAuto("star_fire"); break;
                case "U10": s.AddModifier(src, StatId.MpRegen, mul: 0.2f); break;
                case "U11": s.AddModifier(src, StatId.MoveSpeed, mul: 0.08f); break;
                case "U12": s.AddModifier(src, StatId.MaxHp, add: 15f); b.Heal(15f); break;
                case "U13": s.AddModifier(src, StatId.ResonanceGain, mul: 0.2f); break;
                case "U14": s.AddModifier(src, StatId.ReactionDamage, mul: 0.15f); break;
                case "U15":
                    foreach (var id in new[] { "fire_bolt", "piercing_ice", "split_flame" })
                        Mod(b, id, x => { x.Returns = true; x.Speed *= 0.85f; });
                    break;
                default:
                    return false;
            }
            return true;
        }

        static void Mod(IBuildTarget b, string id, System.Action<RuntimeSpell> change)
        {
            var spell = b.FindSpell(id);
            if (spell == null)
                return;
            change(spell);
            b.RefreshSpell(spell);
        }
    }
}
