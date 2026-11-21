using Starbind.Combat;

namespace Starbind.Items
{
    public enum EquipSlot { Staff, Charm }

    /// <summary>First-version equipment (GDD §9.2): three staffs and two charms. Effects are applied as
    /// StatModifiers under one source id (removable) plus two behaviour flags read by PlayerCombat;
    /// equipment never edits the player's core logic directly.</summary>
    public static class Equipment
    {
        public const string Source = "equipment";

        public struct Def
        {
            public string id, name, description;
            public EquipSlot slot;
        }

        public static readonly Def[] All =
        {
            new Def { id = "sprout_staff", name = "新芽法杖", description = "法杖命中回魔 x2（每秒上限同步提高）", slot = EquipSlot.Staff },
            new Def { id = "moonring_staff", name = "月環法杖", description = "蓄力魔彈到達盡頭後折返（返程 60%）", slot = EquipSlot.Staff },
            new Def { id = "storm_staff", name = "風暴法杖", description = "每第三次有效施法追加一道小電弧", slot = EquipSlot.Staff },
            new Def { id = "ember_charm", name = "餘燼護符", description = "燃燒時間 +25%", slot = EquipSlot.Charm },
            new Def { id = "frost_brooch", name = "霜晶胸針", description = "對冰緩中的敵人傷害 +20%", slot = EquipSlot.Charm },
        };

        public static Def? Find(string id)
        {
            foreach (var d in All)
                if (d.id == id)
                    return d;
            return null;
        }

        /// <summary>Replaces all equipment modifiers on <paramref name="stats"/>.</summary>
        public static void Apply(StatBlock stats, string staff, string charm)
        {
            stats.RemoveSource(Source);
            if (staff == "sprout_staff")
                stats.AddModifier(Source, StatId.ManaOnHit, mul: 1f);
            if (charm == "ember_charm")
                stats.AddModifier(Source, StatId.BurnDuration, mul: 0.25f);
            if (charm == "frost_brooch")
                stats.AddModifier(Source, StatId.ChilledDamage, add: 0.2f);
        }
    }
}
