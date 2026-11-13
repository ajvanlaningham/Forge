using System.Globalization;
using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    public sealed class ProgramService : IProgramService
    {
        private readonly IRepository<ProgramBlockRow> _blocks;
        private readonly IRepository<ProgramDayCompletionRow> _days;
        private readonly IRepository<QuestCompletionRow> _quests;
        private readonly IXpService _xp;

        private readonly SemaphoreSlim _gate = new(1, 1);
        private List<ProgramDefinition>? _catalog;

        public ProgramService(
            IRepository<ProgramBlockRow> blocks,
            IRepository<ProgramDayCompletionRow> days,
            IRepository<QuestCompletionRow> quests,
            IXpService xp)
        {
            _blocks = blocks;
            _days = days;
            _quests = quests;
            _xp = xp;
        }

        // --- catalog -------------------------------------------------------------------------

        private async Task<List<ProgramDefinition>> CatalogAsync()
        {
            if (_catalog is not null) return _catalog;
            var loaded = new List<ProgramDefinition>();
            foreach (var file in GameConstants.Programs.Files)
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(file);
                using var reader = new StreamReader(stream);
                // A bad file is a build-time bug caught by ShippedProgramContentTests; fail loudly.
                loaded.Add(ProgramParser.Parse(await reader.ReadToEndAsync(), file));
            }
            return _catalog = loaded;
        }

        public async Task<IReadOnlyList<ProgramDefinition>> GetProgramsAsync()
            => (await CatalogAsync())
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => p.Version).First())
                .OrderBy(p => p.Family ?? p.Name, StringComparer.CurrentCulture)
                .ThenBy(p => p.TrainingDaysPerWeek)
                .ToList();

        public async Task<ProgramDefinition?> GetProgramAsync(string id, int version)
            => (await CatalogAsync()).FirstOrDefault(p => p.Id == id && p.Version == version);

        private async Task<ProgramDefinition?> LatestAsync(string id)
            => (await CatalogAsync()).Where(p => p.Id == id).OrderByDescending(p => p.Version).FirstOrDefault();

        // --- blocks --------------------------------------------------------------------------

        public async Task<ActiveProgram?> GetActiveAsync()
        {
            await _gate.WaitAsync();
            try { return await GetActiveCoreAsync(); }
            finally { _gate.Release(); }
        }

        private async Task<ActiveProgram?> GetActiveCoreAsync()
        {
            var open = (await _blocks.WhereAsync(b => b.EndedDateKey == null)).OrderByDescending(b => b.Id).FirstOrDefault();
            if (open is null)
            {
                // Auto-start only on a truly fresh install; an ended block with no successor is deliberate.
                if (await _blocks.FirstOrDefaultAsync(b => b.Id > 0) is not null) return null;
                var def = await LatestAsync(GameConstants.Programs.DefaultProgramId);
                if (def is null) return null;
                open = await OpenBlockAsync(def, DateOnly.FromDateTime(DateTime.Today));
            }

            // An active block follows the latest version of its program: content fixes (e.g. the
            // v2 pull-up ladder) reach a block already underway. History is unaffected — every
            // logged session stores the prescription it was performed against.
            var definition = await LatestAsync(open.ProgramId);
            if (definition is not null && definition.Version != open.ProgramVersion)
            {
                open.ProgramVersion = definition.Version;
                await _blocks.UpdateAsync(open);
            }
            if (definition is null)
            {
                // The block's program no longer ships (e.g. a retired placeholder). End it and fall
                // back to the default rather than leaving the user with no program.
                var today = DateOnly.FromDateTime(DateTime.Today);
                open.EndedDateKey = WeekMath.DateKey(today);
                await _blocks.UpdateAsync(open);
                var fallback = await LatestAsync(GameConstants.Programs.DefaultProgramId);
                if (fallback is null) return null;
                var row = await OpenBlockAsync(fallback, today);
                return new ActiveProgram(row.Id, fallback, today);
            }
            return new ActiveProgram(open.Id, definition, ParseKey(open.StartedDateKey));
        }

        public async Task<ActiveProgram> StartProgramAsync(string programId, DateOnly today)
        {
            var def = await LatestAsync(programId) ?? throw new ArgumentException($"Unknown program '{programId}'", nameof(programId));
            await _gate.WaitAsync();
            try
            {
                foreach (var b in await _blocks.WhereAsync(b => b.EndedDateKey == null))
                {
                    b.EndedDateKey = WeekMath.DateKey(today);
                    await _blocks.UpdateAsync(b);
                }
                var row = await OpenBlockAsync(def, today);
                return new ActiveProgram(row.Id, def, today);
            }
            finally { _gate.Release(); }
        }

        private async Task<ProgramBlockRow> OpenBlockAsync(ProgramDefinition def, DateOnly today)
        {
            var row = new ProgramBlockRow { ProgramId = def.Id, ProgramVersion = def.Version, StartedDateKey = WeekMath.DateKey(today) };
            await _blocks.InsertAsync(row);
            return row;
        }

        // --- the block on a date ---------------------------------------------------------------

        private async Task<List<DayCompletion>> TrainingAsync(int blockId)
            => (await _days.WhereAsync(r => r.BlockId == blockId && r.Completed))
                .Select(r => new DayCompletion(r.DayIndex, ParseKey(r.DateKey))).ToList();

        /// <summary>Days with active rest done: the mobility / active-rest quest.</summary>
        private async Task<HashSet<DateOnly>> ActiveRestAsync()
            => (await _quests.WhereAsync(r => r.Kind == (int)QuestKind.Mobility && r.Completed))
                .Select(r => ParseKey(r.DateKey)).ToHashSet();

        public async Task<BlockView?> GetViewAsync(DateOnly date)
        {
            var active = await GetActiveAsync();
            if (active is null) return null;
            var def = active.Definition;

            var training = await TrainingAsync(active.BlockId);
            var offer = BlockSchedule.OfferFor(active.StartedOn, def.TrainingDaysPerWeek, training, date);
            var (wStart, wEnd) = BlockSchedule.WeekRange(active.StartedOn, offer.Week);
            var doneThisWeek = training.Where(c => c.Date >= wStart && c.Date <= wEnd).Select(c => c.DayIndex).ToHashSet();
            var day = offer.TrainingDay is int i ? def.Resolve(i, offer.Week) : null;
            return new BlockView(active, offer.Week, day, offer.TrainingDoneToday, offer.BlockComplete,
                doneThisWeek, await MultiplierForAsync(date));
        }

        /// <summary>
        /// Days that count for the daily streak (Decision 26): a completed training day in any block,
        /// or active rest. Shared by the streak display and the XP multiplier so they never disagree.
        /// </summary>
        public async Task<IReadOnlyList<DateOnly>> ActiveDaysAsync()
            => (await _days.WhereAsync(r => r.Completed)).Select(r => ParseKey(r.DateKey))
                .Concat(await ActiveRestAsync())
                .Distinct().ToList();

        public async Task<IReadOnlyList<MapNode>> GetMapAsync(DateOnly date)
        {
            var active = await GetActiveAsync();
            if (active is null) return [];
            return QuestMap.Build(active.StartedOn, active.Definition.TrainingDaysPerWeek,
                await TrainingAsync(active.BlockId), await ActiveRestAsync(), date);
        }

        public async Task<double> MultiplierForAsync(DateOnly date)
            => StreakMath.Multiplier(await ActiveDaysAsync(), date);

        public async Task CompleteDayAsync(DateOnly date, int? sessionId = null)
        {
            var view = await GetViewAsync(date);
            if (view?.Day is null || view.CompletedOnDate) return;

            var key = WeekMath.DateKey(date);
            var blockId = view.Program.BlockId;
            // Reuse a row left by an un-complete on the same date so its XP bookkeeping carries over.
            var existing = (await _days.WhereAsync(r => r.BlockId == blockId && r.DateKey == key)).FirstOrDefault();
            if (existing is not null)
            {
                existing.Completed = true;
                existing.DayIndex = view.Day.DayIndex;
                existing.SessionId = sessionId ?? existing.SessionId;
                await _days.UpdateAsync(existing);
                return;
            }
            await _days.InsertAsync(new ProgramDayCompletionRow
            {
                BlockId = blockId, DateKey = key, DayIndex = view.Day.DayIndex, Completed = true, SessionId = sessionId
            });
        }

        public async Task UncompleteDayAsync(DateOnly date)
        {
            var active = await GetActiveAsync();
            if (active is null) return;
            var key = WeekMath.DateKey(date);
            foreach (var row in await _days.WhereAsync(r => r.BlockId == active.BlockId && r.DateKey == key && r.Completed))
            {
                row.Completed = false;
                await _days.UpdateAsync(row);
            }
        }

        public async Task<int> ReconcileXpAsync(DateOnly date)
        {
            var key = WeekMath.DateKey(date);
            var rows = await _days.WhereAsync(r => r.DateKey == key); // every block: reclaiming must work after a switch
            if (rows.Count == 0) return 0;

            var active = await GetActiveAsync();
            var award = GameMath.Quests.WithMultiplier(
                GameMath.Quests.XpForTrainingDay(active?.Definition.TrainingDaysPerWeek ?? 4), await MultiplierForAsync(date));

            var total = 0;
            foreach (var row in rows)
            {
                var (entry, delta) = XpLedger.Reconcile(row.Completed, new XpEntry(row.XpGranted, row.XpAwarded), award);
                if (delta != 0)
                {
                    (row.XpGranted, row.XpAwarded) = (entry.Granted, entry.Awarded);
                    await _days.UpdateAsync(row);
                    total += delta;
                }
                if (!row.Completed && !row.XpGranted) await _days.DeleteAsync(row);
            }
            if (total != 0) await _xp.ApplyAsync(total);
            return total;
        }

        public async Task<int> CompletedDaysInBlockAsync(int blockId)
            => (await TrainingAsync(blockId)).Count;

        public async Task<(int DayIndex, int Xp)?> DayCompletedBySessionAsync(int sessionId)
            => (await _days.WhereAsync(r => r.SessionId == sessionId && r.Completed)).FirstOrDefault() is { } row
                ? (row.DayIndex, row.XpAwarded)
                : null;

        private static DateOnly ParseKey(string key) => DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
