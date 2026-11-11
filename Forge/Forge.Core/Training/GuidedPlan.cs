namespace Forge.Training
{
    /// <summary>An exercise as the guided session sees it.</summary>
    /// <param name="Index">Position in the session (stable across the plan).</param>
    /// <param name="Superset">Group key; consecutive exercises sharing one alternate sets.</param>
    /// <param name="Sets">Working sets.</param>
    /// <param name="RestSeconds">Rest after a working set (after a whole round, in a superset).</param>
    /// <param name="Warmups">Built-in warm-up sets before the first working set (0, 1 or 2).</param>
    public sealed record GuidedExercise(int Index, string? Superset, int Sets, int? RestSeconds, int Warmups);

    /// <summary>One warm-up set: load, reps, and its share of the working load (for display).</summary>
    public sealed record WarmupSet(double LoadLb, int Reps, int Percent);

    /// <summary>One set in the guided order, and the rest that follows it.</summary>
    /// <param name="SetNumber">1-based working set number, or 1-based warm-up number for a warm-up.</param>
    public sealed record GuidedStep(int ExerciseIndex, bool IsWarmup, int SetNumber, int SetsTotal, int RestAfterSeconds);

    /// <summary>
    /// The order a guided session runs in: one set at a time, rest, next. Straight sets for lone
    /// exercises; supersets alternate (A1, A2, A1, A2…) with a short transition between partners and
    /// the full rest after each round. Main lifts start with one lighter warm-up set.
    /// </summary>
    public static class GuidedPlan
    {
        /// <summary>Moving between superset partners: enough to swap implements.</summary>
        public const int TransitionSeconds = 30;
        public const int WarmupRestSeconds = 45;
        public const int WarmupReps = 8;
        /// <summary>Warm-up load as a fraction of the working load.</summary>
        public const double WarmupFraction = 0.5;
        /// <summary>The ramp set for heavier lifts: about three quarters, fewer reps.</summary>
        public const double RampFraction = 0.75;
        public const int RampReps = 4;

        /// <summary>Estimated time per rep, for timed sets (the user chose hands-free sets).</summary>
        public const int SecondsPerRep = 3;
        /// <summary>Getting into position before the first rep.</summary>
        public const int SetupSeconds = 5;

        public static IReadOnlyList<GuidedStep> Build(IReadOnlyList<GuidedExercise> exercises, int defaultRestSeconds)
        {
            var steps = new List<(GuidedStep Step, bool EndsRound, int RoundRest)>();
            var i = 0;
            while (i < exercises.Count)
            {
                // A block: one exercise, or a run of consecutive exercises sharing a superset key.
                var block = new List<GuidedExercise> { exercises[i] };
                while (exercises[i].Superset is { } key && i + block.Count < exercises.Count
                       && exercises[i + block.Count].Superset == key)
                    block.Add(exercises[i + block.Count]);
                i += block.Count;

                var roundRest = block.Max(e => e.RestSeconds ?? defaultRestSeconds);
                foreach (var e in block)
                    for (var w = 1; w <= e.Warmups; w++)
                        steps.Add((new GuidedStep(e.Index, true, w, e.Sets, WarmupRestSeconds), true, WarmupRestSeconds));

                var rounds = block.Max(e => e.Sets);
                for (var r = 1; r <= rounds; r++)
                {
                    var members = block.Where(e => e.Sets >= r).ToList();
                    for (var m = 0; m < members.Count; m++)
                    {
                        var e = members[m];
                        var last = m == members.Count - 1;
                        var rest = last ? (block.Count == 1 ? e.RestSeconds ?? defaultRestSeconds : roundRest) : TransitionSeconds;
                        steps.Add((new GuidedStep(e.Index, false, r, e.Sets, rest), last, rest));
                    }
                }
            }
            // Nothing to rest for after the last set.
            if (steps.Count > 0) steps[^1] = (steps[^1].Step with { RestAfterSeconds = 0 }, true, 0);
            return steps.Select(s => s.Step).ToList();
        }

        /// <summary>
        /// Index of the first step not yet done, given what's logged per exercise (warm-ups and
        /// working sets, counted separately). Equal to <c>steps.Count</c> when everything is done.
        /// </summary>
        public static int Position(IReadOnlyList<GuidedStep> steps, Func<int, (int Warmups, int Working)> logged)
        {
            var used = new Dictionary<int, (int W, int S)>();
            for (var k = 0; k < steps.Count; k++)
            {
                var step = steps[k];
                var have = logged(step.ExerciseIndex);
                used.TryGetValue(step.ExerciseIndex, out var u);
                if (step.IsWarmup)
                {
                    if (u.W + 1 > have.Warmups) return k;
                    used[step.ExerciseIndex] = (u.W + 1, u.S);
                }
                else
                {
                    if (u.S + 1 > have.Working) return k;
                    used[step.ExerciseIndex] = (u.W, u.S + 1);
                }
            }
            return steps.Count;
        }

        /// <summary>
        /// Sets where a miss is likely, so the rest screen asks "Got all 8?" instead of assuming:
        /// the last working set of an exercise, or a target at the top of the rep range. Warm-ups and
        /// holds are never asked. Unanswered means yes.
        /// </summary>
        public static bool IsHardSet(GuidedStep step, int? targetReps, int? repsMax)
            => !step.IsWarmup && targetReps is int reps
               && (step.SetNumber == step.SetsTotal || (repsMax is int hi && reps >= hi));

        /// <summary>How long a set takes: a hold's own time, or reps at a steady pace plus setup.</summary>
        public static int SetSeconds(int? reps, int? seconds)
            => seconds is int s ? s + SetupSeconds : (reps ?? 8) * SecondsPerRep + SetupSeconds;

        /// <summary>
        /// The warm-ups for a working load: about 50% × 8, then — when it lands on a weight of its
        /// own, strictly between the first warm-up and the working load — about 75% × 4. Light lifts
        /// get one warm-up; none at all when nothing lighter than the working load is owned.
        /// </summary>
        public static IReadOnlyList<WarmupSet> Warmups(double workingLb, LoadSet loads)
        {
            var sets = new List<WarmupSet>();
            if (workingLb <= 0) return sets;
            var first = WarmupLoad(workingLb, loads);
            if (first >= workingLb) return sets;
            sets.Add(new WarmupSet(first, WarmupReps, Percent(first, workingLb)));
            var ramp = loads.Snap(workingLb * RampFraction);
            if (ramp > first && ramp < workingLb)
                sets.Add(new WarmupSet(ramp, RampReps, Percent(ramp, workingLb)));
            return sets;
        }

        private static int Percent(double lb, double workingLb) => (int)Math.Round(100 * lb / workingLb);

        /// <summary>
        /// The warm-up load: about half the working load, snapped to a weight you own and always
        /// lighter than the working load when anything lighter exists.
        /// </summary>
        public static double WarmupLoad(double workingLb, LoadSet loads)
        {
            if (workingLb <= 0) return 0;
            var half = loads.Snap(workingLb * WarmupFraction);
            if (half >= workingLb) half = loads.Prev(workingLb) ?? workingLb;
            return half;
        }
    }
}
