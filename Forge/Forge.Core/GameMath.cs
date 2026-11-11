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
        /// XP required to advance one level: exactly one week of full adherence (see
        /// <see cref="Quests.AdherentWeekXp"/>). That keeps "retest every 4 levels" roughly monthly.
        /// </summary>
        public const int XpPerLevel = Quests.AdherentWeekXp;

        public static class Quests
        {
            /// <summary>A daily quest: mobility, or the date-generated strength quest when no program is active.</summary>
            public const int XpPerQuest = 50;

            /// <summary>
            /// A program day (a full training session) is substantially more work than a mobility
            /// routine, so it earns three quests' worth.
            /// </summary>
            public const int XpPerProgramDay = 150;

            /// <summary>Bonus for hitting the weekly conditioning-minutes goal.</summary>
            public const int ConditioningWeeklyXp = XpPerQuest * 6;

            public const int ProgramDaysPerWeek = 4;
            public const int MobilityQuestsPerWeek = 7;

            /// <summary>
            /// A fully adherent week: every program day, daily mobility, and the conditioning goal.
            /// 4 × 150 + 7 × 50 + 300 = 1250. Weigh-in XP is deliberately left out — it is slack
            /// on top, not something a level should depend on (weight is its own track).
            /// </summary>
            public const int AdherentWeekXp =
                ProgramDaysPerWeek * XpPerProgramDay
                + MobilityQuestsPerWeek * XpPerQuest
                + ConditioningWeeklyXp;
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
