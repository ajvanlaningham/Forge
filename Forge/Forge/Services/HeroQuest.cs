using Forge.Training;

namespace Forge.Services
{
    /// <summary>
    /// Quests named for the current hero (Decision 22): a Hearth quest in Copper is the "Copper
    /// Starry Hat" for the archmage and the "Copper Heater Shield" for the knight.
    /// </summary>
    public static class HeroQuest
    {
        public static GearSlot Slot(ProgramDefinition def) => Armoury.SlotFor(def.Discipline);

        public static string ItemName(GearSlot slot) => HeroGear.Name(UserSettings.HeroRace, UserSettings.HeroClass, slot);

        public static string ItemName(ProgramDefinition def) => ItemName(Slot(def));

        /// <summary>"Copper Archmage's Staff".</summary>
        public static string Name(ProgramDefinition def) => $"{def.Metal} {ItemName(def)}";

        public static string Sprite(GearSlot slot, string metal) => HeroGear.Sprite(UserSettings.HeroRace, UserSettings.HeroClass, slot, metal);

        public static string Sprite(ProgramDefinition def) => Sprite(Slot(def), def.Metal);
    }
}
