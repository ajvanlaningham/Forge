namespace Forge.Training
{
    /// <summary>One measurement compared with its baseline. Values must be positive.</summary>
    /// <param name="SmallerIsBetter">Heart rate: a lower recovery pulse is fitter.</param>
    public readonly record struct Measure(string Key, double Current, double Baseline, bool SmallerIsBetter = false);

    /// <summary>
    /// Stat scores (Decision 5, protocol v2): relative to your own baseline, on a log scale.
    /// <c>score = 10 + 25 × log2(current / baseline)</c>, clamped 1–100: the baseline scores 10, and
    /// doubling any measure is +25 however strong you started. A stat with several measures (three
    /// lifts and push-ups) averages their log ratios. One retest moves a score up by at most 10.
    /// </summary>
    public static class StatScoring
    {
        public const int BaselineScore = 10;
        public const double PointsPerDoubling = 25;
        public const int MaxGainPerTest = 10;
        public const int Min = 1;
        public const int Max = 100;

        /// <summary>log2 of the improvement ratio, or null when either value can't be compared.</summary>
        public static double? LogRatio(Measure m)
        {
            if (m.Current <= 0 || m.Baseline <= 0) return null;
            return Math.Log2(m.SmallerIsBetter ? m.Baseline / m.Current : m.Current / m.Baseline);
        }

        /// <param name="previousScore">The last scored result, for the +10 cap; null for the baseline.</param>
        public static int Score(IEnumerable<Measure> measures, int? previousScore = null)
        {
            var terms = measures.Select(LogRatio).OfType<double>().ToList();
            if (terms.Count == 0) return previousScore ?? BaselineScore;
            var raw = BaselineScore + PointsPerDoubling * terms.Average();
            if (previousScore is int prev) raw = Math.Min(raw, prev + MaxGainPerTest);
            return (int)Math.Round(Math.Clamp(raw, Min, Max), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Turning raw test results into scorable measures (protocol v2, docs/baseline-tests.md).</summary>
    public static class StatMeasures
    {
        /// <summary>Estimated 1RM per pound of bodyweight (Decision 6). Dumbbell loads are one hand's.</summary>
        public static double RelativeStrength(double loadLb, int reps, double bodyweightLb)
            => bodyweightLb <= 0 || reps <= 0 ? 0 : StrengthMath.Epley(Math.Max(loadLb, 0), reps) / bodyweightLb;

        /// <summary>
        /// Shoulder reach is measured as the gap between the fists (smaller is better, and it can go
        /// negative when they overlap). Scored as reach = 60 − gap, so it stays positive and bigger is better.
        /// </summary>
        public const double ShoulderOffsetCm = 60;
        public static double ShoulderReach(double gapCm) => Math.Max(0.5, ShoulderOffsetCm - gapCm);

        /// <summary>A side-to-side measure (knee-to-wall, shoulder) scores the mean of both sides.</summary>
        public static double BothSides(double left, double right) => (left + right) / 2;
    }
}
