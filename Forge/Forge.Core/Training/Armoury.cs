namespace Forge.Training
{
    /// <summary>The three pieces every hero forges (Decision 22).</summary>
    public enum GearSlot
    {
        /// <summary>The defensive piece: shield, hat, hood, tome… (Hearth quests).</summary>
        Hearth,
        /// <summary>The weapon in hand (Anvil quests).</summary>
        Anvil,
        /// <summary>Light gear: boots, bracers, anklets… (Bellows quests).</summary>
        Bellows
    }

    /// <summary>A forged piece: a slot in a metal. Which item that is depends on the current hero.</summary>
    public readonly record struct Forged(GearSlot Slot, string Metal);

    /// <summary>
    /// The tier ladder and what's been forged on it (Decisions 17, 18, 22). Copper is always open;
    /// forging all three slots in a metal opens the next one.
    /// </summary>
    public static class Armoury
    {
        public static readonly string[] Metals = { "Copper", "Bronze", "Iron", "Steel", "Damascus", "Mithril", "Starmetal" };
        public static readonly GearSlot[] Slots = { GearSlot.Hearth, GearSlot.Anvil, GearSlot.Bellows };

        /// <summary>A quest's discipline decides which slot it forges.</summary>
        public static GearSlot SlotFor(string discipline) => discipline switch
        {
            "Anvil" => GearSlot.Anvil,
            "Bellows" => GearSlot.Bellows,
            _ => GearSlot.Hearth
        };

        public static int TierOf(string metal)
        {
            var i = Array.FindIndex(Metals, m => string.Equals(m, metal, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? 0 : i;
        }

        /// <summary>Slots forged in a metal (each counted once, however many times it was forged).</summary>
        public static IReadOnlySet<GearSlot> ForgedIn(IEnumerable<Forged> forged, string metal)
            => forged.Where(f => TierOf(f.Metal) == TierOf(metal)).Select(f => f.Slot).ToHashSet();

        /// <summary>The highest open tier: Copper, plus one for every metal with all three slots forged in order.</summary>
        public static int OpenTier(IEnumerable<Forged> forged)
        {
            var list = forged.ToList();
            var tier = 0;
            while (tier < Metals.Length - 1 && ForgedIn(list, Metals[tier]).Count == Slots.Length)
                tier++;
            return tier;
        }

        /// <summary>The best metal a slot has been forged in, or null.</summary>
        public static string? BestMetal(IEnumerable<Forged> forged, GearSlot slot)
            => forged.Where(f => f.Slot == slot).OrderByDescending(f => TierOf(f.Metal)).Select(f => Metals[TierOf(f.Metal)]).FirstOrDefault();

        /// <summary>The file-name form of a metal ("copper").</summary>
        public static string Key(string metal) => Metals[TierOf(metal)].ToLowerInvariant();
        public static string Key(GearSlot slot) => slot.ToString().ToLowerInvariant();
    }
}
