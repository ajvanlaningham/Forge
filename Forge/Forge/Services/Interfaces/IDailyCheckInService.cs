using Forge.Models;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Resting HR, sleep and steps per day (PBI 3.4), typed by hand or filled from the phone's
    /// health store. Hand entries always win: a sync only fills fields that are empty or were
    /// themselves synced.
    /// </summary>
    public interface IDailyCheckInService
    {
        Task<DailyCheckInRow> GetAsync(DateOnly date);

        /// <summary>
        /// Saves hand-typed values. A non-null value becomes Manual; a null value clears the
        /// field so the next sync may fill it again.
        /// </summary>
        Task<DailyCheckInRow> SaveManualAsync(DateOnly date, int? restingHr, int? sleepMinutes, int? steps);

        /// <summary>
        /// Reads the health store for these days and merges. Quietly does nothing when the
        /// store is unavailable or nothing is granted. Returns how many fields changed.
        /// </summary>
        Task<int> SyncAsync(params DateOnly[] dates);
    }
}
