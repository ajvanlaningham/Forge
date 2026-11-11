namespace Forge.Constants
{
    /// <summary>
    /// Daily streak (PBI 4.3): consecutive days with at least one completed quest. Levels measure
    /// consistency, and the streak is consistency made visible.
    /// </summary>
    public static class StreakMath
    {
        /// <summary>
        /// Length of the streak as of <paramref name="today"/>. Today not being done <i>yet</i> does
        /// not break it — the streak runs through yesterday until today is over.
        /// </summary>
        public static int Current(IEnumerable<DateOnly> activeDays, DateOnly today)
        {
            var days = activeDays.ToHashSet();
            var cursor = days.Contains(today) ? today : today.AddDays(-1);
            var count = 0;
            while (days.Contains(cursor))
            {
                count++;
                cursor = cursor.AddDays(-1);
            }
            return count;
        }

        /// <summary>Longest run ever, for context next to the current one.</summary>
        public static int Longest(IEnumerable<DateOnly> activeDays)
        {
            var sorted = activeDays.Distinct().OrderBy(d => d).ToList();
            int best = 0, run = 0;
            for (var i = 0; i < sorted.Count; i++)
            {
                run = i > 0 && sorted[i].DayNumber == sorted[i - 1].DayNumber + 1 ? run + 1 : 1;
                best = Math.Max(best, run);
            }
            return best;
        }
    }
}
