namespace Forge.Constants
{
    /// <summary>
    /// Fixed, non-tunable values. Anything the user can change belongs in settings,
    /// not here — see <c>UserSettings</c> in the app project.
    /// </summary>
    public static class GameConstants
    {
        // Storage
        public static class Db
        {
            public const string FileName = "forge.db3";
        }

        // Exercise library
        public static class Exercises
        {
            public const string PrefixKey = "ExerciseLibraryVersion";

            /// <summary>
            /// Every JSON file under <c>Resources/Raw</c> that should be seeded into SQLite.
            /// IMPORTANT: seeding is gated on <see cref="LibraryVersion"/> alone. Adding a file
            /// here without bumping the version will NOT seed it on an existing install — the
            /// importer sees a matching version and returns immediately. Bump the version
            /// whenever this list or any library file changes.
            /// </summary>
            public static readonly string[] LibraryFiles =
            {
                "strength.v1.json",
                "mobility.v1.json",
                "conditioning.v1.json",
                "recovery.v1.json"
            };

            public const string LibraryVersion = "v1";
            public const string ExSourceTag = "recovery";
        }

        /// <summary>
        /// Starting values for user-tunable settings. These are defaults only; the live value
        /// is read from settings so the user can change it without a rebuild.
        /// </summary>
        public static class Defaults
        {
            /// <summary>Weekly cardio goal in minutes (3 hours).</summary>
            public const int WeeklyConditioningGoalMinutes = 180;
        }
    }

    public static class UiConstants
    {
        /// <summary>Display order for equipment groups in the My Gear page.</summary>
        public static readonly string[] EquipmentGroupOrder =
        {
            "Strength", "Conditioning", "Mobility", "Other"
        };
    }
}
