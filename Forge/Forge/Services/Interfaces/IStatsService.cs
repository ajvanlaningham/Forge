using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IStatsService
    {
        /// Ensure local storage is ready (creates DB/tables, seeds defaults).
        Task InitAsync();

        /// Rollup of Level/XP (from the local store).
        Task<UserStats> GetUserStatsAsync();

        /// Core RPG‑style stats (currently placeholder values).
        Task<IReadOnlyList<Stat>> GetCoreStatsAsyncFromDb();
    }
}