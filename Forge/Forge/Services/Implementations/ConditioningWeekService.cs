using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations;
public sealed class ConditioningWeekService : IConditioningWeekService
{
    private readonly IRepository<ConditioningWeekRow> _repo;
    private readonly IXpService _xp;
    private bool _initialized;

    private static int WeeklyXpReward => GameMath.Quests.ConditioningWeeklyXp;

    public ConditioningWeekService(IRepository<ConditioningWeekRow> repo, IXpService xp)
    {
        _repo = repo;
        _xp = xp;
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
        var weekStart = WeekMath.MondayOf(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);
        return ToProgress(row);
    }

    public async Task<WeeklyConditioningProgress> AddConditioningMinutesAsync(DateOnly date, int minutes, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        if (minutes <= 0) return await GetWeekProgressAsync(date, ct);

        var weekStart = WeekMath.MondayOf(date);
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
        var weekStart = WeekMath.MondayOf(date);
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
        var weekStart = WeekMath.MondayOf(date);
        var row = await GetOrCreateRowAsync(weekStart, ct);

        row.Minutes = 0;
        row.XpGranted = false;
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        await _repo.InsertOrReplaceAsync(row);

        return ToProgress(row);
    }

    // --- helpers ---

    private async Task<ConditioningWeekRow> GetOrCreateRowAsync(DateOnly weekStartMonday, CancellationToken ct)
    {
        var key = WeekMath.DateKey(weekStartMonday);
        var existing = (await _repo.WhereAsync(r => r.WeekStartKey == key)).FirstOrDefault();
        if (existing != null) return existing;

        var created = new ConditioningWeekRow
        {
            WeekStartKey = key,
            Minutes = 0,
            GoalMinutes = UserSettings.WeeklyConditioningGoalMinutes,
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
            WeekEnd = WeekMath.SundayOf(monday),
            Minutes = row.Minutes,
            GoalMinutes = row.GoalMinutes
        };
    }

    private async Task MaybeGrantWeeklyXpAsync(ConditioningWeekRow row, CancellationToken ct)
    {
        // The weekly bonus follows the shared rule: paid once when the goal is met. (Minutes only
        // ever go up today, so it's never taken back; the rule would reclaim it if they could.)
        var (entry, delta) = XpLedger.Reconcile(row.Minutes >= row.GoalMinutes && row.GoalMinutes > 0,
            new XpEntry(row.XpGranted, row.XpGranted ? WeeklyXpReward : 0), WeeklyXpReward, legacyAward: WeeklyXpReward);
        if (delta == 0) return;
        row.XpGranted = entry.Granted;
        row.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        await _repo.InsertOrReplaceAsync(row);
        await _xp.ApplyAsync(delta);
    }
}
