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

            public const string LibraryVersion = "v8"; // v8: the Four-Day Home Split's exercises; one-arm row needs no bench
            public const string ExSourceTag = "recovery";
        }

        /// <summary>
        /// Training programs, read straight from the app package on demand (not seeded into SQLite,
        /// so there is no version gate to forget). Keep every published version listed: history
        /// rows point at a specific <c>id@version</c>, and an old version must stay loadable for
        /// its history to display.
        /// </summary>
        public static class Programs
        {
            public static readonly string[] Files =
            {
                "programs/copper-helm-db-4day.v1.json",
                "programs/copper-helm-db-3day.v1.json",
                "programs/copper-helm-kb-4day.v1.json",
                "programs/copper-helm-kb-3day.v1.json",
                // v2: band-assisted pull-ups every week; dead hang in active rest.
                "programs/copper-helm-db-4day.v2.json",
                "programs/copper-helm-db-3day.v2.json",
                "programs/copper-helm-kb-4day.v2.json",
                "programs/copper-helm-kb-3day.v2.json",
                // v3: supersets (A = main lifts, B = accessories) for the guided session
                "programs/copper-helm-db-4day.v3.json",
                "programs/copper-helm-db-3day.v3.json",
                "programs/copper-helm-kb-4day.v3.json",
                "programs/copper-helm-kb-3day.v3.json",
                // The Copper tier's other two quests (2026-11-11): Anvil (strength) and Bellows (conditioning)
                "programs/copper-hammer-db-4day.v1.json",
                "programs/copper-hammer-db-3day.v1.json",
                "programs/copper-hammer-kb-4day.v1.json",
                "programs/copper-hammer-kb-3day.v1.json",
                "programs/copper-boots-db-4day.v1.json",
                "programs/copper-boots-db-3day.v1.json",
                "programs/copper-boots-kb-4day.v1.json",
                "programs/copper-boots-kb-3day.v1.json",
                // A second Hearth quest (2026-11-12): Miche's four-day dumbbell split, as written
                // by them, with supersets added. Forges the same Copper Hearth piece as Copper Helm.
                "programs/copper-split-db-4day.v1.json"
            };

            /// <summary>Program offered when none has been chosen yet.</summary>
            public const string DefaultProgramId = "copper-helm-db-4day";
        }

        /// <summary>
        /// The guild + encrypted-backup server (Decision 24): Forge.Server on the nidavellir LXC,
        /// reached over Tailscale HTTPS. Tailnet-only until Funnel is switched on for it; the URL
        /// is the same either way.
        /// </summary>
        public static class Nidavellir
        {
            public const string BaseUrl = "https://nidavellir.tail625ec6.ts.net/";
        }

        /// <summary>
        /// In-app update delivery. Builds are published as GitHub Releases by CI; the app reads
        /// them straight from the public Releases API.
        /// </summary>
        /// <remarks>
        /// This works precisely because the repository is public — no credentials are involved.
        /// If Forge is ever made private, do NOT solve it by embedding a token in the app; move
        /// the manifest and APK behind something that can hold a secret instead.
        /// </remarks>
        public static class Updates
        {
            public const string GitHubOwner = "ajvanlaningham";
            public const string GitHubRepo = "Forge";

            /// <summary>Latest published release, unauthenticated. Rate limited to 60/hour per IP.</summary>
            public const string LatestReleaseUrl =
                "https://api.github.com/repos/" + GitHubOwner + "/" + GitHubRepo + "/releases/latest";

            /// <summary>Release asset holding the build manifest.</summary>
            public const string ManifestAssetName = "version.json";

            /// <summary>The GitHub API rejects requests without a User-Agent.</summary>
            public const string UserAgent = "Forge-Android-Updater";

            /// <summary>Filename the downloaded APK is cached under.</summary>
            public const string DownloadFileName = "forge-update.apk";
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
