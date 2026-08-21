namespace Forge.Constants
{
    /// <summary>
    /// Progression and scoring math. Pure functions only — no I/O, no MAUI types — so this
    /// is unit-testable on the build host.
    /// </summary>
    /// <remarks>
    /// This class was previously nested as <c>GameMath.GameConstants</c>, which shadowed the
    /// top-level <see cref="GameConstants"/> and forced call sites to read
    /// <c>GameMath.GameConstants.Quests.XpPerQuest</c>. It is flattened deliberately; do not
    /// reintroduce a nested type named <c>GameConstants</c> here.
    /// </remarks>
    public static class GameMath
    {
        /// <summary>
        /// XP required to advance one level: 21 quests per week at 50 XP each.
        /// One level therefore equals one week of full adherence, which makes the
        /// "retest every 4 levels" cadence land at roughly monthly.
        /// </summary>
        public const int XpPerLevel = 1050;

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

        /// <summary>
        /// Fraction of a single level represented by <paramref name="xp"/>, clamped to 0..1.
        /// </summary>
        public static double LevelProgress(int xp, int xpPerLevel = XpPerLevel)
            => Math.Clamp((double)xp / xpPerLevel, 0.0, 1.0);

        /// <summary>Level implied by a lifetime XP total. Level 1 is the starting level.</summary>
        public static int LevelFromXp(int xp, int xpPerLevel = XpPerLevel)
            => (xp / xpPerLevel) + 1;

        /// <summary>XP earned since the start of the current level.</summary>
        public static int XpIntoLevel(int xp, int xpPerLevel = XpPerLevel)
            => xp % xpPerLevel;

        /// <summary>XP still needed to reach the next level.</summary>
        public static int XpToNextLevel(int xp, int xpPerLevel = XpPerLevel)
        {
            var into = XpIntoLevel(xp, xpPerLevel);
            return xpPerLevel - into;
        }

        /// <summary>
        /// Linear stat scoring: +1 point per 10% improvement over baseline, from a floor of 1.
        /// </summary>
        /// <remarks>
        /// Scheduled for replacement — reaching <see cref="Stats.MaxScore"/> on this curve
        /// requires roughly a 990% improvement, so the top of the scale is unreachable.
        /// See PBI 5.1 in TODO.md for the logarithmic replacement.
        /// </remarks>
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
