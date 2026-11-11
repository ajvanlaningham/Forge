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
        private readonly IStatsStore _stats;

        public ActiveRestService(IRepository<QuestCompletionRow> rows, IProgramService programs, IStatsStore stats)
        {
            _rows = rows;
            _programs = programs;
            _stats = stats;
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
            await _stats.InitAsync();
            var user = await _stats.GetUserStatsAsync();
            var changed = false;
            if (row.Completed && !row.XpGranted)
            {
                // ×1.2 while the perfect-week streak is alive; the exact amount is recorded.
                var multiplier = await _programs.MultiplierForAsync(date);
                row.XpAwarded = GameMath.Quests.WithMultiplier(GameMath.Quests.XpPerQuest, multiplier);
                row.XpGranted = true;
                user.Xp += row.XpAwarded;
                net += row.XpAwarded;
                changed = true;
            }
            else if (!row.Completed && row.XpGranted)
            {
                // Rows from before XpAwarded existed hold the base award.
                var amount = row.XpAwarded > 0 ? row.XpAwarded : GameMath.Quests.XpPerQuest;
                user.Xp = Math.Max(0, user.Xp - amount);
                row.XpGranted = false;
                row.XpAwarded = 0;
                net -= amount;
                changed = true;
            }
            if (changed)
            {
                await _rows.UpdateAsync(row);
                await _stats.UpsertUserStatsAsync(user);
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
