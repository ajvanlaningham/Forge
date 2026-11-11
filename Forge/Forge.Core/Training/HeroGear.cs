namespace Forge.Training
{
    public static partial class HeroGear
    {
        /// <summary>"Archmage's Staff" for a human wizard's anvil piece.</summary>
        public static string Name(string race, string cls, GearSlot slot)
            => Names.TryGetValue($"{race}_{cls}_{Armoury.Key(slot)}", out var n) ? n : slot switch
            {
                GearSlot.Anvil => "Weapon",
                GearSlot.Bellows => "Boots",
                _ => "Helm"
            };

        /// <summary>The item's sprite in a metal, e.g. <c>gear_human_wizard_anvil_copper.png</c>.</summary>
        public static string Sprite(string race, string cls, GearSlot slot, string metal)
            => $"gear_{race}_{cls}_{Armoury.Key(slot)}_{Armoury.Key(metal)}.png";
    }
}
