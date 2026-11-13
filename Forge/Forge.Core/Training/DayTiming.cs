namespace Forge.Training
{
    /// <summary>What timing needs to know about an exercise: is it worked one side at a time, and is it loaded.</summary>
    public readonly record struct MoveTraits(bool PerSide, bool Loaded);

    /// <summary>
    /// How long a training day takes, from the timeline the guided session will actually run
    /// (Decision 28): every set at its clock or steady rep pace, every rest, warm-up sets for
    /// counted main lifts. The minutes shown on the map, the quest board and the briefing all come
    /// from here, never from a program's notes.
    /// </summary>
    public static class DayTiming
    {
        public const int DefaultRestSeconds = 60;

        public static int Seconds(ResolvedDay day, Func<string, MoveTraits> traits, int defaultRest = DefaultRestSeconds)
        {
            var slots = day.Slots;
            var exercises = slots.Select((s, i) =>
            {
                var t = traits(s.Slot.Exercise);
                var warmups = s.IsMain && !s.Slot.IsTimed && t.Loaded ? 1 : 0;
                return new GuidedExercise(i, s.Slot.Superset, s.Slot.Sets, s.Slot.RestSeconds, warmups, s.Slot.IsTimed);
            }).ToList();
            var steps = GuidedPlan.Build(exercises, defaultRest);
            return steps.Sum(step =>
            {
                var slot = slots[step.ExerciseIndex].Slot;
                var perSide = traits(slot.Exercise).PerSide;
                var work = step.IsWarmup
                    ? GuidedPlan.SetSeconds(GuidedPlan.WarmupReps, null, perSide)
                    : GuidedPlan.SetSeconds(slot.RepsMin, slot.Seconds, perSide);
                return work + step.RestAfterSeconds;
            });
        }

        public static int Minutes(ResolvedDay day, Func<string, MoveTraits> traits, int defaultRest = DefaultRestSeconds)
            => (int)Math.Round(Seconds(day, traits, defaultRest) / 60.0);

        /// <summary>"31–38": the shortest and longest day across the block, for the quest board.</summary>
        public static string Range(ProgramDefinition program, Func<string, MoveTraits> traits)
        {
            var all = Enumerable.Range(0, program.Days.Count)
                .SelectMany(d => Enumerable.Range(1, ProgramDefinition.WeeksPerBlock).Select(w => Minutes(program.Resolve(d, w), traits)))
                .ToList();
            return all.Count == 0 ? "?" : all.Min() == all.Max() ? $"{all.Min()}" : $"{all.Min()}–{all.Max()}";
        }
    }
}
