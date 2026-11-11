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
        private readonly IProgramService _programs;

        public ForgeService(IRepository<ForgeResultRow> results, IProgramService programs)
        {
            _results = results;
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
                        Slot = (int)Armoury.SlotFor(def.Discipline),
                        Metal = def.Metal,
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
            => (await _results.WhereAsync(r => r.Forged)).Select(r => new Forged((GearSlot)r.Slot, r.Metal)).ToList();
    }
}
