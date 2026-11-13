namespace Forge.Training
{
    /// <summary>The five pieces every hero can forge (Decision 25, 2026-11-13).</summary>
    public enum GearSlot
    {
        Head,
        Body,
        Weapon,
        Feet,
        Trinket
    }

    /// <summary>
    /// One forging spent: a slot raised to a metal. <paramref name="Form"/> is the chosen shape when a
    /// slot offers several (a sword or a sword and shield); null means the hero's default.
    /// </summary>
    public readonly record struct Forged(GearSlot Slot, string Metal, string? Form = null);

    /// <summary>
    /// The metal ladder (Decision 25). Finishing a quest earns one forging; the player picks a slot
    /// and it climbs one metal. Slots climb independently, with no tier gate and no discipline
    /// attached, so any quest can raise any piece. Five slots × seven metals = 35 forgings.
    /// </summary>
    public static class Armoury
    {
        public static readonly string[] Metals = { "Copper", "Bronze", "Iron", "Steel", "Damascus", "Mithril", "Starmetal" };
        public static readonly GearSlot[] Slots = { GearSlot.Head, GearSlot.Body, GearSlot.Weapon, GearSlot.Feet, GearSlot.Trinket };

        public static int MaxForgings => Slots.Length * Metals.Length;

        public static int TierOf(string metal)
        {
            var i = Array.FindIndex(Metals, m => string.Equals(m, metal, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? 0 : i;
        }

        /// <summary>How many times a slot has been forged: 0 is bare, 1 Copper … 7 Starmetal.</summary>
        public static int Level(IEnumerable<Forged> forged, GearSlot slot)
            => Math.Min(Metals.Length, forged.Count(f => f.Slot == slot));

        /// <summary>The slot's current metal, or null while it's bare.</summary>
        public static string? MetalOf(IEnumerable<Forged> forged, GearSlot slot)
            => Level(forged, slot) is var level and > 0 ? Metals[level - 1] : null;

        /// <summary>What the next forging of a slot makes, or null at Starmetal.</summary>
        public static string? NextMetal(IEnumerable<Forged> forged, GearSlot slot)
            => Level(forged, slot) is var level && level < Metals.Length ? Metals[level] : null;

        /// <summary>The form last chosen for a slot, or null for the default.</summary>
        public static string? FormOf(IEnumerable<Forged> forged, GearSlot slot)
            => forged.Where(f => f.Slot == slot).Select(f => f.Form).LastOrDefault();

        /// <summary>The best metal anywhere on the hero, or null when nothing is forged.</summary>
        public static string? BestMetal(IEnumerable<Forged> forged)
        {
            var list = forged.ToList();
            var best = Slots.Max(s => Level(list, s));
            return best > 0 ? Metals[best - 1] : null;
        }

        /// <summary>The file-name form of a metal ("copper").</summary>
        public static string Key(string metal) => Metals[TierOf(metal)].ToLowerInvariant();
        public static string Key(GearSlot slot) => slot.ToString().ToLowerInvariant();

        public static string Label(GearSlot slot) => slot.ToString();
    }
}
