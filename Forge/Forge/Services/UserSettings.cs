using Forge.Constants;

namespace Forge.Services
{
    /// <summary>
    /// User-tunable targets, backed by <see cref="Preferences"/>.
    /// </summary>
    /// <remarks>
    /// These are personal goals, not game rules — they must never be compile-time constants,
    /// because changing one should not require a rebuild and redeploy. Values in
    /// <see cref="GameConstants.Defaults"/> are starting points only.
    /// Weight targets (start / milestone / goal) land here with PBI 3.1.
    /// </remarks>
    public enum ActiveRestStyle { Guided, Freestyle }

    public static class UserSettings
    {
        private const string WeeklyConditioningGoalKey = "Settings.WeeklyConditioningGoalMinutes";
        private const string WeightUnitKey = "Settings.WeightUnit";
        private const string GymEquipmentKey = "Settings.GymEquipment";
        private const string LastLocationKey = "Settings.LastTrainingLocation";
        private const string DefaultRestKey = "Settings.DefaultRestSeconds";
        private const string HomeGearKey = "Settings.HomeGear";

        /// <summary>Weekly cardio goal in minutes. Defaults to 3 hours.</summary>
        public static int WeeklyConditioningGoalMinutes
        {
            get => Preferences.Get(
                WeeklyConditioningGoalKey,
                GameConstants.Defaults.WeeklyConditioningGoalMinutes);
            set => Preferences.Set(WeeklyConditioningGoalKey, Math.Max(0, value));
        }

        /// <summary>
        /// Display unit for weights. Storage is always pounds — this only decides what
        /// number is shown and what unit the entry form accepts. Flipping this does not
        /// migrate stored values.
        /// </summary>
        public static WeightUnit WeightUnit
        {
            get => (WeightUnit)Preferences.Get(WeightUnitKey, (int)WeightUnit.Pounds);
            set => Preferences.Set(WeightUnitKey, (int)value);
        }
    
        /// <summary>
        /// Equipment available at the Gym location. Defaults to everything; Home uses the owned
        /// Inventory instead.
        /// </summary>
        public static Forge.Models.Equipment GymEquipment
        {
            get => (Forge.Models.Equipment)Preferences.Get(GymEquipmentKey, (long)AllEquipment);
            set => Preferences.Set(GymEquipmentKey, (long)value);
        }

        /// <summary>Where the last session was done — the default for the next one.</summary>
        /// <summary>
        /// How a rest day is offered (side quests, 2026-11-13): <b>Guided</b> runs a short mobility
        /// flow in the workout screen; <b>Freestyle</b> just logs your own practice (a seasoned
        /// yogi doesn't need to be told what to do).
        /// </summary>
        public static ActiveRestStyle ActiveRestStyle
        {
            get => (ActiveRestStyle)Preferences.Get("Settings.ActiveRestStyle", (int)ActiveRestStyle.Guided);
            set => Preferences.Set("Settings.ActiveRestStyle", (int)value);
        }

        public static TrainingLocation LastLocation
        {
            get => (TrainingLocation)Preferences.Get(LastLocationKey, (int)TrainingLocation.Gym);
            set => Preferences.Set(LastLocationKey, (int)value);
        }

        /// <summary>Rest between sets when a program slot doesn't specify one.</summary>
        public static int DefaultRestSeconds
        {
            get => Preferences.Get(DefaultRestKey, 90);
            set => Preferences.Set(DefaultRestKey, Math.Clamp(value, 0, 600));
        }

        /// <summary>
        /// The weights owned at Home (Decision 16): dumbbells, kettlebells, barbell. Sections left
        /// unset fall back to plain steps until entered. The Gym is always "everything".
        /// </summary>
        public static Forge.Training.GearLoads HomeGear
        {
            get => Forge.Training.GearLoads.FromJson(Preferences.Get(HomeGearKey, null));
            set => Preferences.Set(HomeGearKey, value.ToJson());
        }

        /// <summary>Loads available at a location.</summary>
        public static Forge.Training.GearLoads GearAt(TrainingLocation location)
            => location == TrainingLocation.Gym ? Forge.Training.GearLoads.Everything(WeightUnit) : HomeGear;

        // --- stat tests (protocol v2, Decision 23): choices frozen at the baseline ---

        /// <summary>The three STR test lifts (squat or lunge, press, row), chosen once from your gear.</summary>
        public static string[]? TestLifts
        {
            get => Preferences.Get("Tests.Lifts", null)?.Split('|');
            set => Preferences.Set("Tests.Lifts", value is null ? null : string.Join('|', value));
        }

        /// <summary>Where the push-up test is done: floor, a box or step, or a wall.</summary>
        public static string? PushupIncline
        {
            get => Preferences.Get("Tests.PushupIncline", null);
            set => Preferences.Set("Tests.PushupIncline", value);
        }

        /// <summary>The step test's step height, in cm, kept for every retest.</summary>
        public static double? StepHeightCm
        {
            get => Preferences.ContainsKey("Tests.StepCm") ? Preferences.Get("Tests.StepCm", 30.0) : null;
            set { if (value is null) Preferences.Remove("Tests.StepCm"); else Preferences.Set("Tests.StepCm", value.Value); }
        }

        // --- comparisons (optional): only to place results against published norms ---

        public static int? BirthYear
        {
            get => Preferences.ContainsKey("Profile.BirthYear") ? Preferences.Get("Profile.BirthYear", 1990) : null;
            set { if (value is null) Preferences.Remove("Profile.BirthYear"); else Preferences.Set("Profile.BirthYear", value.Value); }
        }

        /// <summary>Which published norms to compare with: "women", "men", or "none" (the default).</summary>
        public static string NormsGroup
        {
            get => Preferences.Get("Profile.Norms", "none");
            set => Preferences.Set("Profile.Norms", value);
        }

        /// <summary>
        /// The first-run flow has been completed (or skipped because this install already had data,
        /// or was restored from a backup).
        /// </summary>
        public static bool OnboardingDone
        {
            get => Preferences.Get("Onboarding.Done", false);
            set => Preferences.Set("Onboarding.Done", value);
        }

        // --- the hero's look (race x class sprite, picked on the Hero tab) ---

        public static readonly (string Id, string Label)[] Races =
            { ("human", "Human"), ("elf", "Elf"), ("dwarf", "Dwarf"), ("orc", "Orc"), ("halfling", "Halfling"), ("construct", "Construct") };
        public static readonly (string Id, string Label)[] Classes =
            { ("wizard", "Wizard"), ("warrior", "Warrior"), ("rogue", "Rogue"), ("ranger", "Ranger"), ("cleric", "Cleric"), ("monk", "Monk") };

        /// <summary>Defaults to Construct: the robot wizard the app started with.</summary>
        public static string HeroRace
        {
            get => Valid(Preferences.Get("Hero.Race", "construct"), Races, "construct");
            set => Preferences.Set("Hero.Race", value);
        }

        public static string HeroClass
        {
            get => Valid(Preferences.Get("Hero.Class", "wizard"), Classes, "wizard");
            set => Preferences.Set("Hero.Class", value);
        }

        /// <summary>The sprite file for a race and class (art/gen_characters.py, installed x8).</summary>
        public static string HeroSprite(string race, string cls) => $"char_{race}_{cls}.png";
        public static string HeroSpriteFile => HeroSprite(HeroRace, HeroClass);
        public static string HeroTitle => $"{Label(Races, HeroRace)} {Label(Classes, HeroClass)}";
        /// <summary>"Halfling Ranger" for any race/class ids (a guild member's, say); unknown ids fall back to the id.</summary>
        public static string HeroTitleFor(string race, string cls) => $"{Label(Races, race)} {Label(Classes, cls)}";
        public static bool IsHero(string race, string cls) => Races.Any(r => r.Id == race) && Classes.Any(c => c.Id == cls);

        private static string Valid(string id, (string Id, string Label)[] options, string fallback)
            => options.Any(o => o.Id == id) ? id : fallback;
        private static string Label((string Id, string Label)[] options, string id)
            => options.FirstOrDefault(o => o.Id == id).Label ?? id;

        private static Forge.Models.Equipment AllEquipment =>
            Enum.GetValues<Forge.Models.Equipment>().Aggregate(Forge.Models.Equipment.None, (a, e) => a | e);
    }

    /// <summary>Where a session is done. Decides which equipment is available, and so which swaps apply.</summary>
    public enum TrainingLocation
    {
        Home = 0,
        Gym = 1
    }
}
