using Forge.Models;

namespace Forge.Training
{
    /// <summary>The parts of an exercise that matter for choosing a substitute.</summary>
    public sealed record ExerciseInfo(
        string Name,
        ExerciseCategory Category,
        BodyZone BodyZone,
        MovementPattern Movement,
        Modality Modality,
        Equipment Equipment,
        ActionType Action,
        bool NeedsPair = false);

    /// <summary>
    /// Finds substitutes when a program slot's exercise needs gear the current location doesn't
    /// have (barbell bench at home → dumbbell press). The rotation never breaks over equipment.
    /// </summary>
    public static class ExerciseSwap
    {
        /// <summary>True when every piece of equipment the exercise needs is available.</summary>
        public static bool IsAvailable(ExerciseInfo e, Equipment available)
            => (e.Equipment & ~available) == Equipment.None;

        /// <summary>
        /// Substitutes for <paramref name="target"/>, best first. Only exercises of the same category
        /// and movement pattern that are doable with <paramref name="available"/> qualify — a swap
        /// that trains something else isn't a swap. Ranking: same body zone, then same kind of
        /// action (reps vs. timed), then a loaded modality over bodyweight, then name.
        /// </summary>
        public static IReadOnlyList<ExerciseInfo> Candidates(
            ExerciseInfo target,
            IEnumerable<ExerciseInfo> library,
            Equipment available)
        {
            return library
                .Where(e => e.Name != target.Name
                            && e.Category == target.Category
                            && e.Movement == target.Movement
                            && IsAvailable(e, available))
                .OrderBy(e => e.BodyZone == target.BodyZone ? 0 : 1)
                .ThenBy(e => IsTimed(e.Action) == IsTimed(target.Action) ? 0 : 1)
                .ThenBy(e => LoadRank(e.Modality))
                .ThenBy(e => e.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The exercise to actually do for a slot at a location: the prescribed one if possible,
        /// otherwise the best candidate, otherwise null (the user picks something by hand).
        /// </summary>
        /// <param name="alreadyUsed">
        /// Exercises already in the session. Substitutes skip these, so three presses at a
        /// bodyweight-only location don't all become push-ups. If every candidate is taken, the
        /// result is null and the caller keeps the prescription for the user to sort out.
        /// </param>
        public static ExerciseInfo? Resolve(
            ExerciseInfo prescribed,
            IEnumerable<ExerciseInfo> library,
            Equipment available,
            IReadOnlySet<string>? alreadyUsed = null)
            => IsAvailable(prescribed, available)
                ? prescribed
                : Candidates(prescribed, library, available)
                    .FirstOrDefault(c => alreadyUsed is null || !alreadyUsed.Contains(c.Name));

        private static bool IsTimed(ActionType a) => a is ActionType.Hold or ActionType.Time;

        /// <summary>Prefer free weights, then machines/cables, then bands, then bodyweight.</summary>
        private static int LoadRank(Modality m) => m switch
        {
            Modality.Barbell or Modality.Dumbbell or Modality.Kettlebell => 0,
            Modality.Machine or Modality.Cable => 1,
            Modality.Band => 2,
            _ => 3
        };
    }
}
