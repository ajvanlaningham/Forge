namespace Forge.Training
{
    /// <summary>A program day completed within a block: which day, and on what date.</summary>
    public readonly record struct DayCompletion(int DayIndex, DateOnly Date);

    /// <summary>What the program offers for a given date.</summary>
    /// <param name="DayIndex">Zero-based program day to show.</param>
    /// <param name="CompletedOnDate">True when that day was already completed on this date.</param>
    public readonly record struct ProgramDayOffer(int DayIndex, bool CompletedOnDate);

    /// <summary>
    /// Decides which program day is up. Days advance by <b>completion order, not weekday</b>: a
    /// missed Tuesday means the next session is still Day 2.
    /// </summary>
    /// <remarks>
    /// At most one program day per calendar day. If a day was completed on the date being asked
    /// about, that day is offered (already done) rather than the next one, so the program can't be
    /// rushed through in one sitting and the quest screen keeps showing what was done today.
    /// </remarks>
    public static class ProgramRotation
    {
        /// <param name="dayCount">Number of days in the program (≥ 1).</param>
        /// <param name="completions">Completions in the current block, any order.</param>
        /// <param name="date">The date being displayed.</param>
        public static ProgramDayOffer OfferFor(int dayCount, IEnumerable<DayCompletion> completions, DateOnly date)
        {
            if (dayCount < 1) throw new ArgumentOutOfRangeException(nameof(dayCount), "a program needs at least one day");

            // Only completions on or before the date count: looking back at an earlier date must
            // not be affected by sessions done after it.
            var ordered = completions
                .Where(c => c.Date <= date)
                .OrderBy(c => c.Date)
                .ToList();

            var today = ordered.LastOrDefault(c => c.Date == date);
            if (ordered.Count > 0 && ordered[^1].Date == date)
                return new ProgramDayOffer(Normalize(today.DayIndex, dayCount), CompletedOnDate: true);

            if (ordered.Count == 0)
                return new ProgramDayOffer(0, CompletedOnDate: false);

            var next = Normalize(ordered[^1].DayIndex + 1, dayCount);
            return new ProgramDayOffer(next, CompletedOnDate: false);
        }

        /// <summary>
        /// Wraps an index into range — guards against a program version with fewer days than the
        /// stored history (e.g. after switching programs mid-block, which starts a new block anyway).
        /// </summary>
        private static int Normalize(int index, int dayCount) => ((index % dayCount) + dayCount) % dayCount;
    }
}
