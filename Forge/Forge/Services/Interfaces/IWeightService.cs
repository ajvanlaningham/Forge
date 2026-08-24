using Forge.Models;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Log and read bodyweight entries. Weights are stored in pounds; conversion for display
    /// happens in the ViewModel using <c>Forge.Services.UserSettings.WeightUnit</c>.
    /// </summary>
    public interface IWeightService
    {
        Task InitAsync();

        /// <summary>
        /// Insert-or-replace the entry for <paramref name="date"/> and grant the weight-log XP
        /// on the first log for that day. Re-logging the same day updates the row but does
        /// NOT grant XP again — XP is per day, not per save.
        /// </summary>
        Task<WeightLogResult> LogAsync(DateOnly date, double weightPounds, string? note = null);

        /// <summary>Latest entry by date, or null if the user has never logged.</summary>
        Task<WeightEntryRow?> GetLatestAsync();

        /// <summary>The entry for a specific day, or null if that day has no log.</summary>
        Task<WeightEntryRow?> GetForDateAsync(DateOnly date);

        /// <summary>All entries within the last <paramref name="days"/> ending at <paramref name="today"/> inclusive.</summary>
        Task<IReadOnlyList<WeightEntryRow>> GetRecentAsync(DateOnly today, int days);
    }

    /// <summary>Outcome of a weight log, so the UI can show a confirmation with the XP awarded.</summary>
    public sealed record WeightLogResult(WeightEntryRow Row, int XpAwarded);
}
