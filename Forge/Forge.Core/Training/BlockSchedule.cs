using Forge.Constants;

namespace Forge.Training
{
    /// <summary>A training day completed within a block: which day, and on what date.</summary>
    /// <param name="Via">The side quest that filled this training day ("HEMA"), or null for the quest workout.</param>
    public readonly record struct DayCompletion(int DayIndex, DateOnly Date, string? Via = null);

    /// <summary>What the block offers on a date.</summary>
    /// <param name="Week">1-based week of the block (1–4).</param>
    /// <param name="TrainingDay">The training day to do, or null when this week's training is done.</param>
    /// <param name="TrainingDoneToday">True when <paramref name="TrainingDay"/> was completed on this date.</param>
    /// <param name="BlockComplete">True once the block's four weeks have passed.</param>
    public readonly record struct BlockOffer(int Week, int? TrainingDay, bool TrainingDoneToday, bool BlockComplete);

    /// <summary>
    /// The calendar side of a block (Decision 12): weeks run in 7-day steps from the block's start,
    /// whatever happened; missed days are simply missed. Within a week the training days are
    /// flexible — any days, suggested order, at most one per calendar day — and the remaining days
    /// are active rest.
    /// </summary>
    public static class BlockSchedule
    {
        /// <summary>1-based week containing <paramref name="date"/> (4 once the block has run its course).</summary>
        public static int WeekOf(DateOnly blockStart, DateOnly date)
            => Math.Clamp((date.DayNumber - blockStart.DayNumber) / 7 + 1, 1, ProgramDefinition.WeeksPerBlock);

        public static bool IsComplete(DateOnly blockStart, DateOnly date)
            => date.DayNumber - blockStart.DayNumber >= 7 * ProgramDefinition.WeeksPerBlock;

        /// <summary>First and last date of a block week.</summary>
        public static (DateOnly Start, DateOnly End) WeekRange(DateOnly blockStart, int week)
        {
            var start = blockStart.AddDays(7 * (week - 1));
            return (start, start.AddDays(6));
        }

        public static BlockOffer OfferFor(DateOnly blockStart, int trainingDays, IEnumerable<DayCompletion> completions, DateOnly date)
        {
            if (trainingDays < 1) throw new ArgumentOutOfRangeException(nameof(trainingDays));
            var week = WeekOf(blockStart, date);
            var done = IsComplete(blockStart, date);
            var (start, end) = WeekRange(blockStart, week);

            var thisWeek = completions.Where(c => c.Date >= start && c.Date <= end && c.Date <= date).ToList();
            var today = thisWeek.Where(c => c.Date == date).Select(c => (int?)c.DayIndex).FirstOrDefault();
            if (today is int t) return new BlockOffer(week, t, TrainingDoneToday: true, done);

            var completed = thisWeek.Select(c => c.DayIndex).ToHashSet();
            var next = Enumerable.Range(0, trainingDays).Where(i => !completed.Contains(i)).Select(i => (int?)i).FirstOrDefault();
            return new BlockOffer(week, next, TrainingDoneToday: false, done);
        }

        /// <summary>
        /// A perfect week (Decisions 15, 26): every day of it has movement — a training day or active
        /// rest — and all the week's training days were done. It earns the chest on the map; since
        /// Decision 26 it no longer sets an XP multiplier (the daily streak does).
        /// </summary>
        public static bool IsPerfectWeek(DateOnly blockStart, int week, int trainingDays,
            IReadOnlyCollection<DayCompletion> training, IReadOnlySet<DateOnly> activeRest)
        {
            var (start, end) = WeekRange(blockStart, week);
            var trainingDates = training.Where(c => c.Date >= start && c.Date <= end).ToList();
            if (trainingDates.Select(c => c.DayIndex).Distinct().Count() < trainingDays) return false;
            var moved = trainingDates.Select(c => c.Date).ToHashSet();
            for (var d = start; d <= end; d = d.AddDays(1))
                if (!moved.Contains(d) && !activeRest.Contains(d)) return false;
            return true;
        }
    }
}
