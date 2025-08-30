using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations;
public sealed class ConditioningWeekService : IConditioningWeekService
{
    private readonly IRepository<ConditioningWeekRow> _repo;
    private readonly IStatsStore _stats;
    private bool _initialized;

    private static int WeeklyXpReward => GameMath.GameConstants.Quests.XpPerQuest;

    public ConditioningWeekService(IRepository<ConditioningWeekRow> repo, IStatsStore stats)
    {
        _repo = repo;
        _stats = stats;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _repo.GetAllAsync();
        _initialized = true;
    }

    public async Task<WeeklyConditioningProgress> GetWeekProgressAsync(DateOnly date, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var weekStart = GetMonday(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);
        return ToProgress(row);
    }

    public async Task<WeeklyConditioningProgress> AddConditioningMinutesAsync(DateOnly date, int minutes, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        if (minutes <= 0) return await GetWeekProgressAsync(date, ct);

        var weekStart = GetMonday(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);

        checked
        {
            row.Minutes = Math.Max(0, row.Minutes + minutes);
        }
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;

        // Persist minutes 
        await _repo.InsertOrReplaceAsync(row);

        // Handle XP if completed.
        await MaybeGrantWeeklyXpAsync(row, ct);

        return ToProgress(row);
    }

    public async Task<WeeklyConditioningProgress> SetWeeklyGoalAsync(DateOnly date, int goalMinutes, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var weekStart = GetMonday(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);

        row.GoalMinutes = Math.Max(0, goalMinutes);
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        await _repo.InsertOrReplaceAsync(row);

        // If goal decreased below current minutes and XP wasn’t granted yet, grant now.
        await MaybeGrantWeeklyXpAsync(row, ct);

        return ToProgress(row);
    }

    public async Task<WeeklyConditioningProgress> ResetWeekAsync(DateOnly date, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        var weekStart = GetMonday(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);

        row.Minutes = 0;
        row.XpGranted = false;
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        await _repo.InsertOrReplaceAsync(row);

        return ToProgress(row);
    }

    // --- helpers ---

    private static DateOnly GetMonday(DateOnly anyDay)
    {
        // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        // diff = 0 for Monday, 6 for Sunday, etc.
        int diff = ((int)anyDay.DayOfWeek + 6) % 7;
        return anyDay.AddDays(-diff);
    }

    private static DateOnly GetSunday(DateOnly monday) => monday.AddDays(6);
    private static string Key(DateOnly monday) => monday.ToString("yyyy-MM-dd");


    private async Task<ConditioningWeekRow> GetOrCreateRowAsync(DateOnly weekStartMonday, CancellationToken ct)
    {
        var key = Key(weekStartMonday);
        var existing = (await _repo.WhereAsync(r => r.WeekStartKey == key)).FirstOrDefault();
        if (existing != null) return existing;

        var created = new ConditioningWeekRow
        {
            WeekStartKey = key,
            Minutes = 0,
            GoalMinutes = 180, // default 3h
            XpGranted = false,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            UpdatedUtcTicks = DateTime.UtcNow.Ticks
        };
        await _repo.InsertAsync(created);
        return created;
    }

    private WeeklyConditioningProgress ToProgress(ConditioningWeekRow row)
    {
        var monday = DateOnly.Parse(row.WeekStartKey);
        return new WeeklyConditioningProgress
        {
            WeekStart = monday,
            WeekEnd = GetSunday(monday),
            Minutes = row.Minutes,
            GoalMinutes = row.GoalMinutes
        };
    }

    private async Task MaybeGrantWeeklyXpAsync(ConditioningWeekRow row, CancellationToken ct)
    {
        if (row.XpGranted) return;
        if (row.Minutes < row.GoalMinutes) return;

        await _stats.InitAsync();
        var user = await _stats.GetUserStatsAsync();
        user.Xp += WeeklyXpReward;

        row.XpGranted = true;
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;

        await _repo.InsertOrReplaceAsync(row);
        await _stats.UpsertUserStatsAsync(user);
    }
}
