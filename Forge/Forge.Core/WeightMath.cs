namespace Forge.Constants
{
    /// <summary>
    /// Unit of display for weights. Persistence is always in pounds so the stored value
    /// never depends on the user's current UI toggle — flipping the toggle only changes
    /// the displayed number, never what is written to the database.
    /// </summary>
    public enum WeightUnit
    {
        Pounds = 0,
        Kilograms = 1
    }

    /// <summary>
    /// Pure weight helpers: unit conversion and the 7-day moving average shown on Home.
    /// No I/O, no MAUI types — testable on the build host.
    /// </summary>
    public static class WeightMath
    {
        /// <summary>Standard rounding factor for 1 lb == 0.45359237 kg (NIST).</summary>
        public const double PoundsPerKilogram = 2.2046226218487757;

        public const int MovingAverageWindowDays = 7;

        /// <summary>Number of XP granted for a weight log. Level pacing is set in <see cref="GameMath"/>.</summary>
        public const int XpPerWeightLog = 25;

        public static double PoundsToKilograms(double pounds) => pounds / PoundsPerKilogram;
        public static double KilogramsToPounds(double kilograms) => kilograms * PoundsPerKilogram;

        public static double ToDisplay(double storedPounds, WeightUnit unit)
            => unit == WeightUnit.Kilograms ? PoundsToKilograms(storedPounds) : storedPounds;

        public static double ToStoredPounds(double displayValue, WeightUnit unit)
            => unit == WeightUnit.Kilograms ? KilogramsToPounds(displayValue) : displayValue;

        public static string UnitLabel(WeightUnit unit) => unit == WeightUnit.Kilograms ? "kg" : "lb";

        /// <summary>
        /// Mean of the entries in the last <see cref="MovingAverageWindowDays"/> ending on
        /// <paramref name="today"/> inclusive. Null-dropping: missing days do not count as zero,
        /// they are ignored, and the mean is taken over the days that actually have a log.
        /// Returns null if the window contains no logs at all.
        /// </summary>
        /// <remarks>
        /// The window is exactly <see cref="MovingAverageWindowDays"/> wide regardless of how
        /// long the user has been logging — a brand-new user with two logs sees a two-log
        /// average, not "wait five more days". Entries older than the window or dated after
        /// <paramref name="today"/> are ignored.
        /// </remarks>
        public static double? MovingAverage(
            IEnumerable<(DateOnly Date, double WeightPounds)> entries,
            DateOnly today)
            => MovingAverageWithSample(entries, today) is { } r ? r.Average : null;

        /// <summary>
        /// Same window contract as <see cref="MovingAverage"/> but also reports how many days
        /// contributed. The card UI shows the count in the label ("3-day avg", "1-day avg") so
        /// a brand-new user does not see "7-day avg" over a single log.
        /// </summary>
        public static MovingAverageSample? MovingAverageWithSample(
            IEnumerable<(DateOnly Date, double WeightPounds)> entries,
            DateOnly today)
        {
            var oldest = today.AddDays(-(MovingAverageWindowDays - 1));

            double sum = 0;
            int count = 0;
            foreach (var e in entries)
            {
                if (e.Date < oldest || e.Date > today) continue;
                sum += e.WeightPounds;
                count++;
            }

            return count == 0 ? null : new MovingAverageSample(sum / count, count);
        }
    }

    /// <summary>
    /// Result of <see cref="WeightMath.MovingAverageWithSample"/> — the mean weight (pounds)
    /// and the number of distinct-day logs that contributed to it.
    /// </summary>
    public readonly record struct MovingAverageSample(double Average, int SampleDays);
}
