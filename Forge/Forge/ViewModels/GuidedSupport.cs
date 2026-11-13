using Forge.Models;
using Forge.Training;

namespace Forge.ViewModels
{
    /// <summary>Rules the briefing and the guided session share, so the estimate matches the run.</summary>
    internal static class GuidedSupport
    {
        /// <summary>Loaded modalities: these get a lighter warm-up set. Bodyweight and holds don't.</summary>
        public static bool IsLoaded(ExerciseInfo? info)
            => info?.Modality is Modality.Dumbbell or Modality.Kettlebell or Modality.Barbell or Modality.Cable or Modality.Machine;

        /// <summary>
        /// Main loaded lifts warm up (about 50% × 8, plus about 75% × 4 for heavier ones) when
        /// there's a lighter weight to warm up with.
        /// </summary>
        public static IReadOnlyList<WarmupSet> Warmups(bool isMain, ExerciseInfo? info, bool timed, double loadLb, LoadSet loads)
            => isMain && !timed && IsLoaded(info) ? GuidedPlan.Warmups(loadLb, loads) : Array.Empty<WarmupSet>();

        /// <summary>Total minutes: every set at the guided pace plus every rest.</summary>
        public static int EstimateMinutes(IReadOnlyList<GuidedStep> steps, Func<int, (int? Reps, int? Seconds, bool PerSide)> target)
        {
            var seconds = steps.Sum(s =>
            {
                var (reps, secs, perSide) = target(s.ExerciseIndex);
                var warmupReps = s.SetNumber == 1 ? GuidedPlan.WarmupReps : GuidedPlan.RampReps;
                return (s.IsWarmup ? GuidedPlan.SetSeconds(warmupReps, null, perSide) : GuidedPlan.SetSeconds(reps, secs, perSide)) + s.RestAfterSeconds;
            });
            return (int)Math.Round(seconds / 60.0);
        }

        /// <summary>The prescription is per side: done one side at a time, timed for both.</summary>
        public static bool PerSide(ExerciseInfo? info) => info?.Unilateral ?? false;

        private static Dictionary<string, MoveTraits>? _traits;

        /// <summary>
        /// The library's per-side and loaded flags, for <see cref="DayTiming"/>: every minutes figure
        /// in the app comes from the real timeline (Decision 28). Cached; the library rarely changes.
        /// </summary>
        public static async Task<Func<string, MoveTraits>> TraitsAsync(Services.Interfaces.ITrainingService training)
        {
            if (_traits is null)
            {
                var all = await training.GetLibraryAsync();
                _traits = all.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => new MoveTraits(PerSide(g.First()), IsLoaded(g.First())));
            }
            var traits = _traits;
            return name => traits.GetValueOrDefault(name);
        }

        /// <summary>
        /// "× 10", "× 10 / side", "30 s", "30 s / side"; a paced set (timed, reps suggested,
        /// Decision 28) reads "~10 reps": the clock in the ring shows its time.
        /// </summary>
        public static string WorkLabel(int? reps, int? seconds, bool perSide, int? suggestedReps = null)
            => (seconds is int s ? suggestedReps is int aim ? $"~{aim} reps" : $"{s} s" : $"× {reps}") + (perSide ? " / side" : "");

        /// <summary>"A1", "A2", "B1"…: position within a superset, or null for straight sets.</summary>
        public static string? GroupLabel(IReadOnlyList<string?> groups, int index)
        {
            if (groups[index] is not { } key) return null;
            var n = 1;
            for (var i = index - 1; i >= 0 && groups[i] == key; i--) n++;
            return $"{key}{n}";
        }
    }
}
