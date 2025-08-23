using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IStatsStore
    {
        Task InitAsync();
        Task<UserStats> GetUserStatsAsync();
        Task UpsertUserStatsAsync(UserStats stats);

        Task<IReadOnlyList<Stat>> GetStatsAsync();
        Task<Stat?> GetStatAsync(StatKind kind);
        Task UpsertStatAsync(Stat stat);
        Task UpsertStatsAsync(IEnumerable<Stat> stats);
    }
}