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

        /// <summary>Main loaded lifts warm up, when there's a lighter weight to warm up with.</summary>
        public static bool WantsWarmup(bool isMain, ExerciseInfo? info, bool timed, double loadLb, LoadSet loads)
            => isMain && !timed && IsLoaded(info) && loadLb > 0 && GuidedPlan.WarmupLoad(loadLb, loads) < loadLb;

        /// <summary>Total minutes: every set at the guided pace plus every rest.</summary>
        public static int EstimateMinutes(IReadOnlyList<GuidedStep> steps, Func<int, (int? Reps, int? Seconds)> target)
        {
            var seconds = steps.Sum(s =>
            {
                var (reps, secs) = target(s.ExerciseIndex);
                return (s.IsWarmup ? GuidedPlan.SetSeconds(GuidedPlan.WarmupReps, null) : GuidedPlan.SetSeconds(reps, secs)) + s.RestAfterSeconds;
            });
            return (int)Math.Round(seconds / 60.0);
        }

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
