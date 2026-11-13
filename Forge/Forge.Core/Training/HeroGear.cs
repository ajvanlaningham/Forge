namespace Forge.Training
{
    public static partial class HeroGear
    {
        /// <summary>
        /// Decision 22 drew three pieces per hero: the weapon ("anvil"), light gear ("bellows", worn on
        /// the feet or ankles) and a defensive piece ("hearth") that is really one of four things:
        /// headwear, body wear, a trinket, or a shield. Until the five-slot set is drawn (plan phase
        /// 6), each slot shows the art that genuinely fits it, and nothing where none does.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, GearSlot> HearthSlot = new Dictionary<string, GearSlot>
        {
            // Body: aprons, belts, cuirasses, cloaks, mail, sashes, pauldrons
            ["dwarf_cleric"] = GearSlot.Body, ["dwarf_monk"] = GearSlot.Body, ["elf_warrior"] = GearSlot.Body,
            ["halfling_ranger"] = GearSlot.Body, ["halfling_rogue"] = GearSlot.Body, ["orc_monk"] = GearSlot.Body,
            ["orc_ranger"] = GearSlot.Body, ["orc_warrior"] = GearSlot.Body,
            // Trinkets: beads, bracers, brooch, pendant, tome, talisman
            ["construct_monk"] = GearSlot.Trinket, ["elf_monk"] = GearSlot.Trinket, ["elf_ranger"] = GearSlot.Trinket,
            ["halfling_cleric"] = GearSlot.Trinket, ["human_cleric"] = GearSlot.Trinket, ["human_monk"] = GearSlot.Trinket,
            ["orc_cleric"] = GearSlot.Trinket,
            // Shields: a weapon form (sword and shield) once forms exist; shown with the weapon for now
            ["construct_warrior"] = GearSlot.Weapon, ["construct_wizard"] = GearSlot.Weapon,
            ["halfling_warrior"] = GearSlot.Weapon, ["human_warrior"] = GearSlot.Weapon,
            // Everything else is headwear.
        };

        /// <summary>Which slot a hero's defensive piece belongs to.</summary>
        public static GearSlot HearthSlotOf(string race, string cls) => HearthSlot.GetValueOrDefault($"{race}_{cls}", GearSlot.Head);

        private static string? ArtKey(string race, string cls, GearSlot slot) => slot switch
        {
            GearSlot.Weapon => "anvil",
            GearSlot.Feet => "bellows",
            _ when HearthSlotOf(race, cls) == slot => "hearth",
            _ => null
        };

        /// <summary>"Archmage's Staff" for a human wizard's weapon.</summary>
        public static string Name(string race, string cls, GearSlot slot)
            => ArtKey(race, cls, slot) is { } key && Names.TryGetValue($"{race}_{cls}_{key}", out var n) ? n : slot switch
            {
                GearSlot.Head => "Helm",
                GearSlot.Body => "Armour",
                GearSlot.Weapon => "Weapon",
                GearSlot.Feet => "Boots",
                _ => "Trinket"
            };

        /// <summary>The item's sprite in a metal, e.g. <c>gear_human_wizard_anvil_copper.png</c>; null without art.</summary>
        public static string? Sprite(string race, string cls, GearSlot slot, string metal)
            => ArtKey(race, cls, slot) is { } key ? $"gear_{race}_{cls}_{key}_{Armoury.Key(metal)}.png" : null;
    }
}
