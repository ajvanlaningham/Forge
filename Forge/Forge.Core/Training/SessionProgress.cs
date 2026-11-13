namespace Forge.Training
{
    /// <summary>One exercise as the day's health bar sees it.</summary>
    public readonly record struct ExerciseProgress(int SetsTotal, int SetsDone, bool Skipped);

    /// <summary>A segment of the health bar: where it sits (0–1) and how full it still is.</summary>
    public readonly record struct HealthSegment(double Start, double Width, double Remaining, bool Skipped);

    /// <summary>The ring around the countdown: how much of it is left, and for a per-side set, which side.</summary>
    public readonly record struct RingState(double Remaining, SetSide? Side);

    /// <summary>
    /// Workout progress as game pictures (user, 2026-11-13). The day is a foe whose health bar has one
    /// section per working set of the whole session; each set done empties the next section from the
    /// right, so the bar goes down in order however the sets are interleaved (supersets alternate
    /// exercises, but the bar never shows gaps). A skipped exercise's sets turn grey at the far end
    /// and leave the fight: no penalty colour. The countdown sits in a ring that drains with the clock.
    /// </summary>
    public static class SessionProgress
    {
        public static IReadOnlyList<HealthSegment> Segments(IReadOnlyList<ExerciseProgress> exercises)
        {
            var live = exercises.Where(e => !e.Skipped).Sum(e => Math.Max(0, e.SetsTotal));
            var skipped = exercises.Where(e => e.Skipped).Sum(e => Math.Max(0, e.SetsTotal));
            var total = live + skipped;
            if (total == 0) return [];
            var left = live - Done(exercises);
            var width = 1.0 / total;
            return Enumerable.Range(0, total)
                .Select(i => new HealthSegment(i * width, width, i < left ? 1 : 0, i >= live))
                .ToList();
        }

        private static int Done(IReadOnlyList<ExerciseProgress> exercises)
            => exercises.Where(e => !e.Skipped).Sum(e => Math.Clamp(e.SetsDone, 0, Math.Max(0, e.SetsTotal)));

        /// <summary>The foe's health left, 0–1: working sets still to do, out of those not skipped.</summary>
        public static double Remaining(IReadOnlyList<ExerciseProgress> exercises)
        {
            var live = exercises.Where(e => !e.Skipped).Sum(e => Math.Max(0, e.SetsTotal));
            return live == 0 ? 0 : (live - Done(exercises)) / (double)live;
        }

        public static bool Vanquished(IReadOnlyList<ExerciseProgress> exercises) => Remaining(exercises) <= 0;

        /// <summary>
        /// The ring for <paramref name="elapsed"/> of <paramref name="total"/> seconds. A per-side set
        /// (<paramref name="sideSeconds"/> &gt; 0) reports the side, as <see cref="GuidedPlan.SideAt"/>.
        /// </summary>
        public static RingState Ring(double elapsed, double total, int sideSeconds = 0)
        {
            var remaining = total <= 0 ? 0 : Math.Clamp(1.0 - elapsed / total, 0, 1);
            SetSide? side = sideSeconds > 0 ? GuidedPlan.SideAt((int)Math.Floor(elapsed), sideSeconds) : null;
            return new RingState(remaining, side);
        }
    }
}
