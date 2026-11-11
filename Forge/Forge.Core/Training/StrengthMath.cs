namespace Forge.Training
{
    public enum PrKind
    {
        /// <summary>Heaviest load ever used for a working set of this exercise.</summary>
        HeaviestLoad,
        /// <summary>Best estimated one-rep max (Epley).</summary>
        BestE1rm,
        /// <summary>Most reps ever done at a load that had been used before.</summary>
        MostRepsAtLoad
    }

    public sealed record PersonalRecord(PrKind Kind, double LoadLb, int Reps, double Value);

    /// <summary>Estimated 1RM and personal-record detection (PBI 8.6).</summary>
    /// <remarks>
    /// Records are information, not rewards: they earn no XP and the UI presents them neutrally.
    /// That's deliberate — rewarding PRs encourages chasing them, and chasing them is how people
    /// get hurt. Revisit only as a conscious product decision (see TODO.md).
    /// </remarks>
    public static class StrengthMath
    {
        /// <summary>
        /// Epley: <c>w × (1 + reps / 30)</c>. A single is its own 1RM. Shared with baseline
        /// testing (PBI 5.2). Unreliable past ~12 reps, like every rep-max formula — fine for
        /// tracking a trend, not for programming a max attempt.
        /// </summary>
        public static double Epley(double load, int reps)
        {
            if (load <= 0 || reps <= 0) return 0;
            return reps == 1 ? load : load * (1 + reps / 30.0);
        }

        public static double BestE1rm(IEnumerable<WorkSet> sets)
            => sets.Where(s => s.Reps is not null).Select(s => Epley(s.LoadLb, s.Reps!.Value)).DefaultIfEmpty(0).Max();

        /// <summary>
        /// Records set in <paramref name="session"/> against all <paramref name="previous"/> working
        /// sets. The very first session for an exercise sets a baseline, not records — there is
        /// nothing to beat. At most one record of each kind per session (the best one).
        /// </summary>
        public static IReadOnlyList<PersonalRecord> FindRecords(IReadOnlyList<WorkSet> previous, IReadOnlyList<WorkSet> session)
        {
            var prior = previous.Where(s => s.Reps is > 0).ToList();
            var now = session.Where(s => s.Reps is > 0).ToList();
            if (prior.Count == 0 || now.Count == 0) return Array.Empty<PersonalRecord>();

            var records = new List<PersonalRecord>();

            // Heaviest load (meaningless for bodyweight-only work).
            var priorMaxLoad = prior.Max(s => s.LoadLb);
            var heaviest = now.OrderByDescending(s => s.LoadLb).ThenByDescending(s => s.Reps).First();
            if (heaviest.LoadLb > 0 && heaviest.LoadLb > priorMaxLoad)
                records.Add(new PersonalRecord(PrKind.HeaviestLoad, heaviest.LoadLb, heaviest.Reps!.Value, heaviest.LoadLb));

            // Best estimated 1RM.
            var priorBest = BestE1rm(prior);
            var best = now.Select(s => (Set: s, E: Epley(s.LoadLb, s.Reps!.Value))).OrderByDescending(x => x.E).First();
            if (best.E > 0 && best.E > priorBest + 1e-9)
                records.Add(new PersonalRecord(PrKind.BestE1rm, best.Set.LoadLb, best.Set.Reps!.Value, best.E));

            // Most reps at a load used before (includes bodyweight, load 0).
            var priorRepsAt = prior.GroupBy(s => s.LoadLb).ToDictionary(g => g.Key, g => g.Max(s => s.Reps!.Value));
            var repsPr = now
                .Where(s => priorRepsAt.TryGetValue(s.LoadLb, out var max) && s.Reps > max)
                .OrderByDescending(s => s.LoadLb).ThenByDescending(s => s.Reps)
                .FirstOrDefault();
            if (repsPr != default)
                records.Add(new PersonalRecord(PrKind.MostRepsAtLoad, repsPr.LoadLb, repsPr.Reps!.Value, repsPr.Reps!.Value));

            return records;
        }
    }
}
