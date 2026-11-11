using Forge.Constants;

namespace Forge.Training
{
    /// <summary>A working set as progression sees it. Warm-ups never reach this type.</summary>
    public readonly record struct WorkSet(double LoadLb, int? Reps, int? Seconds);

    /// <summary>What the prescription asks for.</summary>
    public readonly record struct Prescription(int Sets, int? RepsMin, int? RepsMax, int? Seconds)
    {
        public bool IsTimed => Seconds is not null && RepsMin is null;
    }

    public enum SuggestionKind
    {
        /// <summary>Hit the top of the range on every set: add load.</summary>
        IncreaseLoad,
        /// <summary>In range: same load, chase another rep.</summary>
        AddReps,
        /// <summary>Missed the bottom of the range: same load, try again.</summary>
        Hold,
        /// <summary>Missed the bottom of the range several sessions running.</summary>
        ConsiderDeload,
        /// <summary>Bodyweight at the top of the range: reps alone have run out.</summary>
        BodyweightTopOfRange,
        /// <summary>Timed exercise: add seconds.</summary>
        AddTime,
        /// <summary>Top of the range at the heaviest load you own: reps are the only way up.</summary>
        AtHeaviestOwned
    }

    public sealed record Suggestion(SuggestionKind Kind, double LoadLb, int? Reps, int? Seconds, string Reason);

    /// <summary>
    /// Double progression (PBI 8.5): work within a rep range at a fixed load; when every working
    /// set reaches the top of the range, the next load goes up one increment and reps reset to
    /// the bottom. Missing the bottom holds the load. Repeated misses only <i>hint</i> at a deload —
    /// nothing is ever changed automatically, and the suggestion is a pre-fill, never a constraint.
    /// </summary>
    public static class Progression
    {
        /// <summary>Consecutive missed sessions at the same load before hinting at a deload.</summary>
        public const int MissesBeforeDeloadHint = 2;

        /// <summary>Seconds added to a timed exercise once every set hits the target.</summary>
        public const int TimeStepSeconds = 5;

        /// <summary>A load increase above this fraction is called out as a big jump.</summary>
        public const double BigJump = 0.15;

        /// <param name="rx">The current prescription (which may differ from when history was logged).</param>
        /// <param name="history">Working sets per past session, <b>most recent first</b>.</param>
        /// <param name="loads">The loads actually available (Decision 16): the next weight is the next
        /// one you own, not an abstract increment.</param>
        /// <param name="unit">Display unit, for the reason text.</param>
        /// <returns>Null with no usable history: there is nothing to progress from.</returns>
        public static Suggestion? Suggest(
            Prescription rx,
            IReadOnlyList<IReadOnlyList<WorkSet>> history,
            LoadSet loads,
            WeightUnit unit)
        {
            var last = history.FirstOrDefault(s => s.Count > 0);
            if (last is null) return null;

            if (rx.IsTimed) return SuggestTime(rx, last);
            if (rx.RepsMin is not int lo || rx.RepsMax is not int hi) return null;

            // Judge the heaviest load used last time; lighter sets were back-offs or ramp-ups.
            var top = last.Max(s => s.LoadLb);
            var atTop = last.Where(s => s.LoadLb == top && s.Reps is not null).ToList();
            if (atTop.Count == 0) return null;
            var minReps = atTop.Min(s => s.Reps!.Value);

            var fullSession = atTop.Count >= rx.Sets;
            var allAtTop = atTop.All(s => s.Reps >= hi);

            if (fullSession && allAtTop)
            {
                if (top <= 0)
                    return new Suggestion(SuggestionKind.BodyweightTopOfRange, 0, hi,
                        null, $"All sets hit {hi}. Add load or a harder variation.");

                if (loads.Next(top) is not double next)
                    return new Suggestion(SuggestionKind.AtHeaviestOwned, top, hi, null,
                        $"All sets hit {hi} at your heaviest weight. Add reps or slow the tempo — or it's time for heavier equipment.");

                // A big jump (e.g. 35 → 45 lb dumbbells, +29%) gets a gentler target.
                var jump = (next - top) / top;
                var reason = jump > BigJump
                    ? $"All {atTop.Count} sets hit {hi}. Next weight up is a big jump (+{jump:P0}) — aim for {lo}, fewer is fine at first."
                    : $"All {atTop.Count} sets hit {hi} last time — add weight, back to {lo}.";
                return new Suggestion(SuggestionKind.IncreaseLoad, next, lo, null, reason);
            }

            if (minReps < lo)
            {
                var misses = history.TakeWhile(s => s.Count > 0 && MissedAt(s, top, lo)).Count();
                return misses >= MissesBeforeDeloadHint
                    ? new Suggestion(SuggestionKind.ConsiderDeload, top, lo, null,
                        $"Below {lo} reps {misses} sessions running. Consider dropping the weight.")
                    : new Suggestion(SuggestionKind.Hold, top, lo, null,
                        $"Missed {lo} last time — same weight, aim for {lo}.");
            }

            var target = Math.Min(hi, minReps + 1);
            return new Suggestion(SuggestionKind.AddReps, top, target, null,
                fullSession
                    ? $"Same weight — aim for {target} on every set."
                    : $"Finish all {rx.Sets} sets at this weight before adding more.");
        }

        private static Suggestion SuggestTime(Prescription rx, IReadOnlyList<WorkSet> last)
        {
            var target = rx.Seconds!.Value;
            var load = last.Max(s => s.LoadLb);
            var held = last.Where(s => s.Seconds is not null).Select(s => s.Seconds!.Value).DefaultIfEmpty(0).Min();
            var allHit = last.Count >= rx.Sets && held >= target;
            var next = allHit ? Math.Max(target, held) + TimeStepSeconds : Math.Max(target, held);
            return new Suggestion(SuggestionKind.AddTime, load, null, next,
                allHit ? $"Held {held}s on every set — go for {next}s." : $"Aim for {next}s on every set.");
        }

        private static bool MissedAt(IReadOnlyList<WorkSet> session, double load, int repsMin)
            => session.Any(s => s.LoadLb == load && s.Reps < repsMin);

    }
}
