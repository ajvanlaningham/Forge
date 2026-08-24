using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class WeightService : IWeightService
    {
        private readonly IRepository<WeightEntryRow> _repo;
        private readonly IStatsStore _stats;

        public WeightService(IRepository<WeightEntryRow> repo, IStatsStore stats)
        {
            _repo = repo;
            _stats = stats;
        }

        public async Task InitAsync()
        {
            await _repo.EnsureTableAsync();
            await _stats.InitAsync();
        }

        public async Task<WeightLogResult> LogAsync(DateOnly date, double weightPounds, string? note = null)
        {
            await InitAsync();

            var key = WeekMath.DateKey(date);
            var existing = await _repo.FirstOrDefaultAsync(r => r.DateKey == key);

            var row = existing ?? new WeightEntryRow { DateKey = key };
            row.WeightPounds = weightPounds;
            row.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            row.LoggedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _repo.InsertOrReplaceAsync(row);

            // XP is per day, not per save — overwriting an existing entry must not grant a
            // second helping. Consistent with the "one entry per DateKey" primary key.
            int xpAwarded = 0;
            if (existing is null)
            {
                var user = await _stats.GetUserStatsAsync();
                user.Xp += WeightMath.XpPerWeightLog;
                await _stats.UpsertUserStatsAsync(user);
                xpAwarded = WeightMath.XpPerWeightLog;
            }

            return new WeightLogResult(row, xpAwarded);
        }

        public async Task<WeightEntryRow?> GetLatestAsync()
        {
            await InitAsync();
            var all = await _repo.GetAllAsync();
            // DateKey is yyyy-MM-dd, so ordinal descending sort == newest first.
            return all
                .OrderByDescending(r => r.DateKey, StringComparer.Ordinal)
                .ThenByDescending(r => r.LoggedAtUnix)
                .FirstOrDefault();
        }

        public async Task<WeightEntryRow?> GetForDateAsync(DateOnly date)
        {
            await InitAsync();
            var key = WeekMath.DateKey(date);
            return await _repo.FirstOrDefaultAsync(r => r.DateKey == key);
        }

        public async Task<IReadOnlyList<WeightEntryRow>> GetRecentAsync(DateOnly today, int days)
        {
            await InitAsync();
            // sqlite-net-pcl's expression-tree translator doesn't cover string.Compare, and
            // the row count here is at most one per day — filter in memory.
            var oldest = today.AddDays(-(days - 1));
            var all = await _repo.GetAllAsync();
            return all
                .Where(r => DateOnly.TryParseExact(r.DateKey, "yyyy-MM-dd", out var d)
                            && d >= oldest && d <= today)
                .OrderBy(r => r.DateKey, StringComparer.Ordinal)
                .ToList();
        }
    }
}
