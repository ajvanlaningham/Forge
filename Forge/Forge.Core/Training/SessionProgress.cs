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
    /// segment per exercise; every working set knocks off its share, so the bar drops set by set and
    /// an exercise's segment is empty when it's done. A skipped exercise greys out instead of
    /// draining: no penalty colour. The countdown sits in a ring that drains with the clock.
    /// </summary>
    public static class SessionProgress
    {
        public static IReadOnlyList<HealthSegment> Segments(IReadOnlyList<ExerciseProgress> exercises)
        {
            if (exercises.Count == 0) return [];
            var width = 1.0 / exercises.Count;
            return exercises.Select((e, i) => new HealthSegment(i * width, width,
                e.Skipped || e.SetsTotal <= 0 ? 0 : 1.0 - Math.Min(e.SetsDone, e.SetsTotal) / (double)e.SetsTotal,
                e.Skipped)).ToList();
        }

        /// <summary>The foe's health left, 0–1, counting only exercises not skipped.</summary>
        public static double Remaining(IReadOnlyList<ExerciseProgress> exercises)
        {
            var live = exercises.Where(e => !e.Skipped && e.SetsTotal > 0).ToList();
            if (live.Count == 0) return 0;
            return live.Average(e => 1.0 - Math.Min(e.SetsDone, e.SetsTotal) / (double)e.SetsTotal);
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
