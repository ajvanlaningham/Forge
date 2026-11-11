using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class ActiveRestService : IActiveRestService
    {
        private readonly IRepository<QuestCompletionRow> _rows;
        private readonly IProgramService _programs;
        private readonly IXpService _xp;

        public ActiveRestService(IRepository<QuestCompletionRow> rows, IProgramService programs, IXpService xp)
        {
            _rows = rows;
            _programs = programs;
            _xp = xp;
        }

        public async Task<bool> IsDoneAsync(DateOnly date)
            => (await RowAsync(date))?.Completed ?? false;

        public async Task<int> SetDoneAsync(DateOnly date, bool done)
        {
            var row = await RowAsync(date);
            if (row is null)
            {
                row = new QuestCompletionRow { DateKey = WeekMath.DateKey(date), Kind = (int)QuestKind.Mobility };
                await _rows.InsertAsync(row);
            }
            row.Completed = done;
            await _rows.UpdateAsync(row);

            var net = await _programs.ReconcileXpAsync(date);
            // ×1.2 while the perfect-week streak is alive; the exact amount is recorded and reclaimed.
            var award = GameMath.Quests.WithMultiplier(GameMath.Quests.XpPerQuest, await _programs.MultiplierForAsync(date));
            var (entry, delta) = XpLedger.Reconcile(row.Completed, new XpEntry(row.XpGranted, row.XpAwarded), award,
                legacyAward: GameMath.Quests.XpPerQuest);
            if (delta != 0)
            {
                (row.XpGranted, row.XpAwarded) = (entry.Granted, entry.Awarded);
                await _rows.UpdateAsync(row);
                await _xp.ApplyAsync(delta);
                net += delta;
            }
            return net;
        }

        private async Task<QuestCompletionRow?> RowAsync(DateOnly date)
        {
            var key = WeekMath.DateKey(date);
            return (await _rows.WhereAsync(r => r.DateKey == key && r.Kind == (int)QuestKind.Mobility)).FirstOrDefault();
        }
    }
}
