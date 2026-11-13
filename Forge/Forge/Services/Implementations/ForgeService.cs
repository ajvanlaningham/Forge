using Forge.Common;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.Services.Implementations
{
    public sealed class ForgeService : IForgeService
    {
        private readonly IRepository<ForgeResultRow> _results;
        private readonly IRepository<ForgingRow> _forgings;
        private readonly IProgramService _programs;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ForgeService(IRepository<ForgeResultRow> results, IRepository<ForgingRow> forgings, IProgramService programs)
        {
            _results = results;
            _forgings = forgings;
            _programs = programs;
        }

        public async Task<ForgeResultRow?> JudgeIfDueAsync(DateOnly today)
        {
            var view = await _programs.GetViewAsync(today);
            if (view is { BlockComplete: true })
            {
                var blockId = view.Program.BlockId;
                if (await _results.FirstOrDefaultAsync(r => r.BlockId == blockId) is null)
                {
                    var def = view.Program.Definition;
                    var done = await _programs.CompletedDaysInBlockAsync(blockId);
                    await _results.InsertAsync(new ForgeResultRow
                    {
                        BlockId = blockId,
                        ProgramId = def.Id,
                        Forged = Forging.Forges(done, def.TrainingDaysPerWeek),
                        DaysDone = done,
                        DaysNeeded = Forging.Needed(def.TrainingDaysPerWeek),
                        DaysTotal = Forging.TotalDays(def.TrainingDaysPerWeek),
                        DateKey = WeekMath.DateKey(today)
                    });
                }
            }
            return (await _results.WhereAsync(r => !r.Seen)).OrderBy(r => r.Id).FirstOrDefault();
        }

        public async Task MarkSeenAsync(int resultId)
        {
            if (await _results.GetByIdAsync(resultId) is { } row)
            {
                row.Seen = true;
                await _results.UpdateAsync(row);
            }
        }

        public Task<ForgeResultRow?> GetResultAsync(int blockId)
            => _results.FirstOrDefaultAsync(r => r.BlockId == blockId);

        public async Task<IReadOnlyList<Forged>> GetForgedAsync()
            => (await _forgings.GetAllAsync()).OrderBy(r => r.Id)
                .Select(r => new Forged((GearSlot)r.Slot, r.Metal, r.Form)).ToList();

        public async Task<IReadOnlyList<ForgeResultRow>> UnspentAsync()
        {
            var spent = (await _forgings.GetAllAsync()).Select(r => r.BlockId).ToHashSet();
            return (await _results.WhereAsync(r => r.Forged)).Where(r => !spent.Contains(r.BlockId)).OrderBy(r => r.Id).ToList();
        }

        public async Task<string?> SpendAsync(int blockId, GearSlot slot, string? form = null)
        {
            await _gate.WaitAsync();
            try
            {
                if (await _results.FirstOrDefaultAsync(r => r.BlockId == blockId && r.Forged) is null) return null;
                if (await _forgings.FirstOrDefaultAsync(r => r.BlockId == blockId) is not null) return null;
                var metal = Armoury.NextMetal(await GetForgedAsync(), slot);
                if (metal is null) return null;
                await _forgings.InsertAsync(new ForgingRow
                {
                    BlockId = blockId, Slot = (int)slot, Metal = metal, Form = form,
                    DateKey = WeekMath.DateKey(DateOnly.FromDateTime(DateTime.Today))
                });
                return metal;
            }
            finally { _gate.Release(); }
        }
    }
}
