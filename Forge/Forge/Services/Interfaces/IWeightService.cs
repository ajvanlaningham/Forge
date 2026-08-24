using Forge.Models;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Log and read bodyweight entries. Weights are stored in pounds; conversion for display
    /// happens in the ViewModel using <c>Forge.Services.UserSettings.WeightUnit</c>.
    /// The service only exposes the <see cref="WeightEntry"/> domain model — the SQLite row
    /// stays inside the persistence layer.
    /// </summary>
    public interface IWeightService
    {
        Task InitAsync();

        /// <summary>
        /// Insert-or-replace the entry for <paramref name="date"/> and grant the weight-log XP.
        /// Every successful save awards <see cref="Forge.Constants.WeightMath.XpPerWeightLog"/> XP,
        /// including corrections to a day that was already logged — the reward is per save, not
        /// per day.
        /// </summary>
        Task<WeightLogResult> LogAsync(DateOnly date, decimal weightPounds, string? note = null);

        /// <summary>Latest entry by date, or null if the user has never logged.</summary>
        Task<WeightEntry?> GetLatestAsync();

        /// <summary>The entry for a specific day, or null if that day has no log.</summary>
        Task<WeightEntry?> GetForDateAsync(DateOnly date);

        /// <summary>All entries within the last <paramref name="days"/> ending at <paramref name="today"/> inclusive.</summary>
        Task<IReadOnlyList<WeightEntry>> GetRecentAsync(DateOnly today, int days);
    }

    /// <summary>Outcome of a weight log, so the UI can show a confirmation with the XP awarded.</summary>
    public sealed record WeightLogResult(WeightEntry Entry, int XpAwarded);
}
