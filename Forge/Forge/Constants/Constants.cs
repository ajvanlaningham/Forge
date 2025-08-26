
namespace Forge.Constants
{
    public static class GameConstants
    {
        // Leveling
        public const int XpPerLevel = 1000;

        // Stat scoring
        public static class Stats
        {
            public const int MinScore = 1;
            public const int MaxScore = 20;
            public const double ScoreStepRatio = 0.10; // ±10% ⇒ ±1 step
        }

        // Storage
        public static class Db
        {
            public const string FileName = "forge.db3";
        }

        // Exercise library
        public static class Exercises
        {
            public const string PrefixKey = "ExerciseLibraryVersion";
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
    }

    public static class UiConstants
    {
        /// <summary>Display order for equipment groups in the My Gear page.</summary>
        public static readonly string[] EquipmentGroupOrder =
        {
            "Strength", "Conditioning", "Mobility", "Other"
        };
    }

    public static class GameMath
    {

        public static class GameConstants
        {
            //  Progression
            public const int XpPerLevel = 1050; // 21 quests * 50 XP

            public static class Quests
            {
                public const int XpPerQuest = 50;
                public const int QuestsPerDay = 3;
                public const int QuestsPerWeek = 21;
            }

            public static class Stats
            {
                public const int MinScore = 1;
                public const int MaxScore = 100;
                public const double ScoreStepRatio = 0.1; 
            }

            public static double LevelProgress(int xp, int xpPerLevel = XpPerLevel)
            => Math.Clamp((double)xp / xpPerLevel, 0.0, 1.0);

            public static int LevelFromXp(int xp, int xpPerLevel = XpPerLevel)
            => (xp / xpPerLevel) + 1;

            public static int XpIntoLevel(int xp, int xpPerLevel = XpPerLevel)
            => xp % xpPerLevel;

            public static int XpToNextLevel(int xp, int xpPerLevel = XpPerLevel)
            {
                var into = XpIntoLevel(xp, xpPerLevel);
                return xpPerLevel - into;
            }

            public static int ScoreFrom(double baseline, double current, bool inverse = false)
            {
                if (baseline <= 0 || current <= 0) return Stats.MinScore;

                var ratio = inverse ? (baseline / current) : (current / baseline);
                var steps = Math.Round((ratio - 1.0) / Stats.ScoreStepRatio);
                var raw = Stats.MinScore + (int)steps;

                return Math.Clamp(raw, Stats.MinScore, Stats.MaxScore);
            }
        }
    }
}
