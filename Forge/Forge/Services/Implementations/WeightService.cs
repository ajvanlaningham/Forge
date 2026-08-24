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

        public async Task<WeightLogResult> LogAsync(DateOnly date, decimal weightPounds, string? note = null)
        {
            await InitAsync();

            var key = WeekMath.DateKey(date);
            var existing = await _repo.FirstOrDefaultAsync(r => r.DateKey == key);

            var row = existing ?? new WeightEntryRow { DateKey = key };
            row.WeightPounds = weightPounds;
            row.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            row.LoggedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await _repo.InsertOrReplaceAsync(row);

            var user = await _stats.GetUserStatsAsync();
            user.Xp += WeightMath.XpPerWeightLog;
            await _stats.UpsertUserStatsAsync(user);

            return new WeightLogResult(ToDomain(row), WeightMath.XpPerWeightLog);
        }

        public async Task<WeightEntry?> GetLatestAsync()
        {
            await InitAsync();
            var all = await _repo.GetAllAsync();
            // DateKey is yyyy-MM-dd, so ordinal descending sort == newest first.
            var latest = all
                .OrderByDescending(r => r.DateKey, StringComparer.Ordinal)
                .ThenByDescending(r => r.LoggedAtUnix)
                .FirstOrDefault();
            return latest is null ? null : ToDomain(latest);
        }

        public async Task<WeightEntry?> GetForDateAsync(DateOnly date)
        {
            await InitAsync();
            var key = WeekMath.DateKey(date);
            var row = await _repo.FirstOrDefaultAsync(r => r.DateKey == key);
            return row is null ? null : ToDomain(row);
        }

        public async Task<IReadOnlyList<WeightEntry>> GetRecentAsync(DateOnly today, int days)
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
                .Select(ToDomain)
                .ToList();
        }

        private static WeightEntry ToDomain(WeightEntryRow row) => new()
        {
            Date = DateOnly.ParseExact(row.DateKey, "yyyy-MM-dd"),
            Pounds = row.WeightPounds,
            Note = row.Note,
            LoggedAt = DateTimeOffset.FromUnixTimeSeconds(row.LoggedAtUnix),
        };
    }
}
