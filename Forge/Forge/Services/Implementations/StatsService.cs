using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    /// <summary>
    /// SQLite-backed stats service: initializes storage, returns rollup & per-stat values.
    /// Score computations can be added later when you log new measurements.
    /// </summary>
    public class StatsService : IStatsService
    {
        private readonly IStatsStore _store;

        public StatsService() : this(new StatsStore()) { }

        // DI-friendly
        public StatsService(IStatsStore store) => _store = store;

        public Task InitAsync() => _store.InitAsync();

        public Task<UserStats> GetUserStatsAsync() => _store.GetUserStatsAsync();

        // Pull current STR/DEX/CON (seeded at Score=1) from the DB
        public Task<IReadOnlyList<Stat>> GetCoreStatsAsyncFromDb() => _store.GetStatsAsync();
    }
}
