using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IConditioningWeekService
    {
        
        Task InitializeAsync(CancellationToken ct = default);

        Task<WeeklyConditioningProgress> GetWeekProgressAsync(DateOnly date, CancellationToken ct = default);

        Task<WeeklyConditioningProgress> AddConditioningMinutesAsync(DateOnly date, int minutes, CancellationToken ct = default);

        Task<WeeklyConditioningProgress> SetWeeklyGoalAsync(DateOnly date, int goalMinutes, CancellationToken ct = default);

        Task<WeeklyConditioningProgress> ResetWeekAsync(DateOnly date, CancellationToken ct = default);
    }
}