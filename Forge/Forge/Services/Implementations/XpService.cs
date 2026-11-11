using Forge.Constants;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class XpService : IXpService
    {
        private readonly IStatsStore _stats;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public XpService(IStatsStore stats) => _stats = stats;

        public async Task<int> GetTotalAsync()
        {
            await _stats.InitAsync();
            return (await _stats.GetUserStatsAsync()).Xp;
        }

        public async Task<int> ApplyAsync(int delta)
        {
            await _gate.WaitAsync();
            try
            {
                await _stats.InitAsync();
                var user = await _stats.GetUserStatsAsync();
                if (delta == 0) return user.Xp;
                user.Xp = XpLedger.Apply(user.Xp, delta);
                await _stats.UpsertUserStatsAsync(user);
                return user.Xp;
            }
            finally { _gate.Release(); }
        }
    }
}
