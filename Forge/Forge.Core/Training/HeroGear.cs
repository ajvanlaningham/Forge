namespace Forge.Training
{
    public static partial class HeroGear
    {
        /// <summary>
        /// The drawn art each slot borrows until the five-slot gear set is drawn (Decision 25, plan
        /// phase 6). Decision 22 drew three pieces per hero: the weapon ("anvil"), a defensive piece
        /// ("hearth": a hat, hood, shield or tome) and light gear ("bellows"). Body and Trinket have
        /// no art yet.
        /// </summary>
        private static string? ArtKey(GearSlot slot) => slot switch
        {
            GearSlot.Weapon => "anvil",
            GearSlot.Feet => "bellows",
            GearSlot.Head => "hearth",
            _ => null
        };

        /// <summary>"Archmage's Staff" for a human wizard's weapon.</summary>
        public static string Name(string race, string cls, GearSlot slot)
            => ArtKey(slot) is { } key && Names.TryGetValue($"{race}_{cls}_{key}", out var n) ? n : slot switch
            {
                GearSlot.Head => "Helm",
                GearSlot.Body => "Armour",
                GearSlot.Weapon => "Weapon",
                GearSlot.Feet => "Boots",
                _ => "Trinket"
            };

        /// <summary>The item's sprite in a metal, e.g. <c>gear_human_wizard_anvil_copper.png</c>; null without art.</summary>
        public static string? Sprite(string race, string cls, GearSlot slot, string metal)
            => ArtKey(slot) is { } key ? $"gear_{race}_{cls}_{key}_{Armoury.Key(metal)}.png" : null;
    }
}
