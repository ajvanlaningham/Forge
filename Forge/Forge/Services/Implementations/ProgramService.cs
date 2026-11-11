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
        private readonly IStatsStore _stats;

        private readonly SemaphoreSlim _gate = new(1, 1);
        private List<ProgramDefinition>? _catalog;

        public ProgramService(
            IRepository<ProgramBlockRow> blocks,
            IRepository<ProgramDayCompletionRow> days,
            IStatsStore stats)
        {
            _blocks = blocks;
            _days = days;
            _stats = stats;
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
                // Parse validates structure; a bad file is a build-time bug caught by
                // ShippedProgramContentTests, so let it throw loudly here too.
                loaded.Add(ProgramParser.Parse(await reader.ReadToEndAsync(), file));
            }
            return _catalog = loaded;
        }

        public async Task<IReadOnlyList<ProgramDefinition>> GetProgramsAsync()
            => (await CatalogAsync())
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => p.Version).First())
                .OrderBy(p => p.Name, StringComparer.CurrentCulture)
                .ToList();

        public async Task<ProgramDefinition?> GetProgramAsync(string id, int version)
            => (await CatalogAsync()).FirstOrDefault(p => p.Id == id && p.Version == version);

        private async Task<ProgramDefinition?> LatestAsync(string id)
            => (await CatalogAsync()).Where(p => p.Id == id).OrderByDescending(p => p.Version).FirstOrDefault();

        // --- blocks --------------------------------------------------------------------------

        public async Task<ActiveProgram?> GetActiveAsync()
        {
            await _gate.WaitAsync();
            try
            {
                return await GetActiveCoreAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ActiveProgram?> GetActiveCoreAsync()
        {
            var open = (await _blocks.WhereAsync(b => b.EndedDateKey == null))
                .OrderByDescending(b => b.Id)
                .FirstOrDefault();

            if (open is null)
            {
                // Only auto-start on a truly fresh install. If the user has ever had a block, an
                // absence of an open one is deliberate and must not be overridden.
                var any = await _blocks.FirstOrDefaultAsync(b => b.Id > 0);
                if (any is not null) return null;

                var def = await LatestAsync(GameConstants.Programs.DefaultProgramId);
                if (def is null) return null;
                open = await OpenBlockAsync(def, DateOnly.FromDateTime(DateTime.Today));
            }

            var definition = await GetProgramAsync(open.ProgramId, open.ProgramVersion)
                             ?? await LatestAsync(open.ProgramId);
            if (definition is null) return null; // program removed from the app package

            return new ActiveProgram(open.Id, definition, ParseKey(open.StartedDateKey));
        }

        public async Task<ActiveProgram> StartProgramAsync(string programId, DateOnly today)
        {
            var def = await LatestAsync(programId)
                      ?? throw new ArgumentException($"Unknown program '{programId}'", nameof(programId));

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
            finally
            {
                _gate.Release();
            }
        }

        private async Task<ProgramBlockRow> OpenBlockAsync(ProgramDefinition def, DateOnly today)
        {
            var row = new ProgramBlockRow
            {
                ProgramId = def.Id,
                ProgramVersion = def.Version,
                StartedDateKey = WeekMath.DateKey(today)
            };
            await _blocks.InsertAsync(row);
            return row;
        }

        // --- days ----------------------------------------------------------------------------

        public async Task<ProgramDayView?> GetDayForDateAsync(DateOnly date)
        {
            var active = await GetActiveAsync();
            if (active is null) return null;

            var rows = await _days.WhereAsync(r => r.BlockId == active.BlockId && r.Completed);
            var completions = rows.Select(r => new DayCompletion(r.DayIndex, ParseKey(r.DateKey)));
            var offer = ProgramRotation.OfferFor(active.Definition.Days.Count, completions, date);

            return new ProgramDayView(active, offer.DayIndex, active.Definition.Days[offer.DayIndex], offer.CompletedOnDate);
        }

        public async Task CompleteDayAsync(DateOnly date, int? sessionId = null)
        {
            var view = await GetDayForDateAsync(date);
            if (view is null || view.CompletedOnDate) return;

            var key = WeekMath.DateKey(date);
            var blockId = view.Program.BlockId;

            // Reuse a row left behind by an un-complete on the same date, so the XP flags carry over.
            var existing = (await _days.WhereAsync(r => r.BlockId == blockId && r.DateKey == key)).FirstOrDefault();
            if (existing is not null)
            {
                existing.Completed = true;
                existing.DayIndex = view.DayIndex;
                existing.SessionId = sessionId ?? existing.SessionId;
                await _days.UpdateAsync(existing);
                return;
            }

            await _days.InsertAsync(new ProgramDayCompletionRow
            {
                BlockId = blockId,
                DateKey = key,
                DayIndex = view.DayIndex,
                Completed = true,
                SessionId = sessionId
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
            // Every block, not just the active one: un-completing a day from a block that has since
            // been ended must still reclaim its XP.
            var rows = await _days.WhereAsync(r => r.DateKey == key);

            await _stats.InitAsync();
            var user = await _stats.GetUserStatsAsync();
            var delta = 0;

            foreach (var row in rows)
            {
                if (row.Completed && !row.XpGranted)
                {
                    row.XpGranted = true;
                    delta += GameMath.Quests.XpPerProgramDay;
                    await _days.UpdateAsync(row);
                }
                else if (!row.Completed && row.XpGranted)
                {
                    row.XpGranted = false;
                    delta -= GameMath.Quests.XpPerProgramDay;
                    await _days.UpdateAsync(row);
                }

                // Fully reverted rows carry no information; drop them.
                if (!row.Completed && !row.XpGranted)
                    await _days.DeleteAsync(row);
            }

            if (delta != 0)
            {
                user.Xp = Math.Max(0, user.Xp + delta);
                await _stats.UpsertUserStatsAsync(user);
            }
            return delta;
        }

        private static DateOnly ParseKey(string key)
            => DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
