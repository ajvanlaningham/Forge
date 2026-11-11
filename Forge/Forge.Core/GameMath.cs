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
        /// <see cref="Quests.AdherentWeekXp"/>). Perfect weeks (×1.2) level a little faster.
        /// </summary>
        public const int XpPerLevel = Quests.AdherentWeekXp;

        public static class Quests
        {
            /// <summary>An active-rest day (or the mobility quest when no program is active).</summary>
            public const int XpPerQuest = 50;

            /// <summary>
            /// XP a fully adherent week earns from training + active rest, before the conditioning
            /// bonus. Shared by every program so a 3-day block levels as fast as a 4-day one
            /// (Decision 13): the training-day award scales with the number of training days.
            /// </summary>
            public const int WeekBudgetXp = 800;

            /// <summary>Bonus for hitting the weekly conditioning-minutes goal.</summary>
            public const int ConditioningWeeklyXp = XpPerQuest * 6;

            /// <summary>×1.2 on XP while the perfect-week streak is alive (Decision 15).</summary>
            public const double PerfectWeekMultiplier = 1.2;

            /// <summary>A fully adherent week: 800 from training + active rest, + 300 conditioning = 1100.</summary>
            public const int AdherentWeekXp = WeekBudgetXp + ConditioningWeeklyXp;

            /// <summary>
            /// XP for one training day in a program with <paramref name="trainingDays"/> per week:
            /// whatever the week budget leaves after the active-rest days. 3-day → 200, 4-day → 162.
            /// </summary>
            public static int XpForTrainingDay(int trainingDays)
            {
                if (trainingDays is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(trainingDays));
                return (WeekBudgetXp - (7 - trainingDays) * XpPerQuest) / trainingDays;
            }

            /// <summary>Applies the perfect-week multiplier, rounding to a whole XP.</summary>
            public static int WithMultiplier(int baseXp, double multiplier) => (int)Math.Round(baseXp * multiplier);
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
