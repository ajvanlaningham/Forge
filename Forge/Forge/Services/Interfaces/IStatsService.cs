using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IStatsService
    {
        Task InitAsync();
        Task<UserStats> GetUserStatsAsync();
        Task<IReadOnlyList<Stat>> GetCoreStatsAsyncFromDb();
    }
}