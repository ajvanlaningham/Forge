using System.Text.Json;
using Forge.Common;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    public sealed class StatTestService : IStatTestService
    {
        private readonly IRepository<StatTestRow> _tests;
        private readonly IProgramService _programs;
        private readonly IStatsStore _stats;

        public StatTestService(IRepository<StatTestRow> tests, IProgramService programs, IStatsStore stats)
        {
            _tests = tests;
            _programs = programs;
            _stats = stats;
        }

        public async Task<DueTest?> DueTodayAsync(DateOnly today)
        {
            var view = await _programs.GetViewAsync(today);
            if (view?.Day is not { } day || view.BlockComplete) return null;
            if (view.Program.Definition.TestFor(day.DayIndex) is not { } slotted) return null;
            var kind = slotted switch
            {
                StatTestKind.Strength => StatKind.Strength,
                StatTestKind.Mobility => StatKind.Dexterity,
                _ => StatKind.Constitution
            };
            var blockId = view.Program.BlockId;
            var inBlock = (await _tests.WhereAsync(t => t.Kind == (int)kind && t.BlockId == blockId)).ToList();

            if (view.Week == ProgramDefinition.LightWeek)
                return inBlock.Any(t => !t.Calibration) ? null : new DueTest(kind, Calibration: false);
            // Week 1: a practice run, until there's a baseline (first quest only, in practice).
            if (view.Week == 1 && !await HasBaselineAsync(kind) && !inBlock.Any(t => t.Calibration))
                return new DueTest(kind, Calibration: true);
            return null;
        }

        public async Task<bool> HasBaselineAsync(StatKind kind)
            => await _tests.FirstOrDefaultAsync(t => t.Kind == (int)kind && !t.Calibration) is not null;

        public async Task<TestOutcome> RecordAsync(StatKind kind, bool calibration, IReadOnlyDictionary<string, double> measures,
            IReadOnlyCollection<string> smallerIsBetter, IReadOnlyDictionary<string, string> detail, double bodyweightLb)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var active = await _programs.GetActiveAsync();
            var scored = (await _tests.WhereAsync(t => t.Kind == (int)kind && !t.Calibration)).OrderBy(t => t.Id).ToList();
            var baseline = scored.FirstOrDefault();
            var previous = scored.LastOrDefault();

            var score = 0;
            if (!calibration)
            {
                if (baseline is null) score = StatScoring.BaselineScore;
                else
                {
                    var baseMeasures = JsonSerializer.Deserialize<Dictionary<string, double>>(baseline.MeasuresJson) ?? new();
                    // Only measures present in both count: a changed test lift is a new baseline for that lift.
                    var compared = measures.Where(m => baseMeasures.ContainsKey(m.Key))
                        .Select(m => new Measure(m.Key, m.Value, baseMeasures[m.Key], smallerIsBetter.Contains(m.Key)));
                    score = StatScoring.Score(compared, previous?.Score);
                }
            }

            await _tests.InsertAsync(new StatTestRow
            {
                Kind = (int)kind,
                DateKey = WeekMath.DateKey(today),
                BlockId = active?.BlockId,
                Calibration = calibration,
                MeasuresJson = JsonSerializer.Serialize(measures),
                DetailJson = JsonSerializer.Serialize(detail),
                BodyweightLb = bodyweightLb,
                Score = score
            });

            if (!calibration)
            {
                await _stats.InitAsync();
                await _stats.UpsertStatAsync(new Stat { Kind = kind, Score = score, Current = score, Baseline = StatScoring.BaselineScore });
            }
            return new TestOutcome(kind, calibration, IsBaseline: !calibration && baseline is null, score,
                Change: calibration || previous is null ? 0 : score - previous.Score);
        }

        public async Task<IReadOnlyList<StatTestRow>> HistoryAsync(StatKind kind)
            => (await _tests.WhereAsync(t => t.Kind == (int)kind)).OrderBy(t => t.Id).ToList();
    }
}
