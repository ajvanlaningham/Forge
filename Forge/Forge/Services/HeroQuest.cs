using Forge.Training;

namespace Forge.Services
{
    /// <summary>
    /// Forged pieces named and drawn for the current hero (Decisions 22, 25): the Weapon slot is the
    /// "Archmage's Staff" for the archmage and a sword for the knight. Quests have their own names now.
    /// </summary>
    public static class HeroQuest
    {
        public static string ItemName(GearSlot slot) => HeroGear.Name(UserSettings.HeroRace, UserSettings.HeroClass, slot);

        /// <summary>The piece's sprite in a metal, or null while the slot has no art.</summary>
        public static string? Sprite(GearSlot slot, string metal) => HeroGear.Sprite(UserSettings.HeroRace, UserSettings.HeroClass, slot, metal);
    }
}
