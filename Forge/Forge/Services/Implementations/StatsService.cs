using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    /// <summary>
    /// Orchestrates business logic on top of the stats store.
    /// </summary>
    public class StatsService : IStatsService
    {
        private readonly IStatsStore _store;

        // DI-only
        public StatsService(IStatsStore store) => _store = store;

        public Task InitAsync() => _store.InitAsync();
        public Task<UserStats> GetUserStatsAsync() => _store.GetUserStatsAsync();
        public Task<IReadOnlyList<Stat>> GetCoreStatsAsyncFromDb() => _store.GetStatsAsync();
    }
}
