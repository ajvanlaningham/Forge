using Forge.Models;
using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>A test due today: which stat, and whether it's the unscored practice run.</summary>
    public sealed record DueTest(StatKind Kind, bool Calibration);

    /// <summary>The result of recording a test: the new score and how far it moved.</summary>
    public sealed record TestOutcome(StatKind Kind, bool Calibration, bool IsBaseline, int Score, int Change);

    /// <summary>
    /// Stat tests, protocol v2 (Decision 23, docs/baseline-tests.md). Tests fall on their slotted
    /// training days: a practice run in week 1 of the first quest, then every Honing week. Scores
    /// are relative to your own baseline (StatScoring).
    /// </summary>
    public interface IStatTestService
    {
        /// <summary>The test due on today's training day, if any.</summary>
        Task<DueTest?> DueTodayAsync(DateOnly today);

        /// <summary>Whether a scored baseline exists for a stat (so the next test isn't a practice run).</summary>
        Task<bool> HasBaselineAsync(StatKind kind);

        /// <summary>
        /// Records a test. <paramref name="measures"/> are scorable (positive; key → value),
        /// <paramref name="smallerIsBetter"/> lists keys where lower wins (recovery pulse).
        /// </summary>
        Task<TestOutcome> RecordAsync(StatKind kind, bool calibration, IReadOnlyDictionary<string, double> measures,
            IReadOnlyCollection<string> smallerIsBetter, IReadOnlyDictionary<string, string> detail, double bodyweightLb);

        Task<IReadOnlyList<StatTestRow>> HistoryAsync(StatKind kind);
    }
}
