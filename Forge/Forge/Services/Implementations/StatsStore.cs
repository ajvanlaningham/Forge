using SQLite;
using Forge.Services.Interfaces;
using Forge.Models;
using Forge.Contstants;
using Forge.Data;

namespace Forge.Services.Implementations
{
    public sealed class StatsStore : IStatsStore
    {
        private readonly IRepository<UserStats> _userRepo;
        private readonly IRepository<StatRow> _statRepo;

        public StatsStore(IRepository<UserStats> userRepo, IRepository<StatRow> statRepo)
        {
            _userRepo = userRepo;
            _statRepo = statRepo;
        }

        public async Task InitAsync()
        {
            // Ensure tables exist (idempotent)
            await _userRepo.EnsureTableAsync();
            await _statRepo.EnsureTableAsync();

            // Seed single UserStats
            var userStats = await _userRepo.FirstOrDefaultAsync(_ => true);
            if (userStats is null)
            {
                userStats = new UserStats();
                await _userRepo.InsertAsync(userStats);
            }

            // Seed STR/DEX/CON if empty — Score = 1
            var anyStat = await _statRepo.FirstOrDefaultAsync(_ => true);
            if (anyStat is null)
            {
                var rows = new[]
                {
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Strength,     Baseline = 0, Current = 0, Unit = "×BW",      Score = 1 },
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Dexterity,    Baseline = 0, Current = 0, Unit = "s",        Score = 1 },
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Constitution, Baseline = 0, Current = 0, Unit = "km/10min", Score = 1 },
                };
                await _statRepo.InsertAllAsync(rows);
            }
        }

        public async Task<UserStats> GetUserStatsAsync()
        {
            var user = await _userRepo.FirstOrDefaultAsync(_ => true);
            if (user is null)
            {
                user = new UserStats();
                await _userRepo.InsertAsync(user);
            }
            return user;
        }

        public async Task UpsertUserStatsAsync(UserStats stats)
        {
            stats.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            stats.IsDirty = true;
            await _userRepo.InsertOrReplaceAsync(stats);
        }

        // ---- Stats ----
        public async Task<IReadOnlyList<Stat>> GetStatsAsync()
        {
            var rows = await _statRepo.GetAllAsync();
            return rows.Select(ToDomain).ToList();
        }

        public async Task<Stat?> GetStatAsync(StatKind kind)
        {
            var row = await _statRepo.FirstOrDefaultAsync(r => r.Kind == (int)kind);
            return row is null ? null : ToDomain(row);
        }

        public async Task UpsertStatAsync(Stat stat)
        {
            var user = await GetUserStatsAsync();
            var existing = await _statRepo.FirstOrDefaultAsync(r => r.Kind == (int)stat.Kind);

            if (existing is null)
            {
                await _statRepo.InsertAsync(new StatRow
                {
                    UserStatsId = user.Id,
                    Kind = (int)stat.Kind,
                    Baseline = stat.Baseline,
                    Current = stat.Current,
                    Unit = stat.Unit,
                    Score = stat.Score <= 0 ? 1 : stat.Score
                });
            }
            else
            {
                existing.Baseline = stat.Baseline;
                existing.Current = stat.Current;
                existing.Unit = stat.Unit;
                existing.Score = stat.Score <= 0 ? 1 : stat.Score;
                await _statRepo.UpdateAsync(existing);
            }
        }

        public async Task UpsertStatsAsync(IEnumerable<Stat> stats)
        {
            foreach (var s in stats) await UpsertStatAsync(s);
        }

        private static Stat ToDomain(StatRow row) => new()
        {
            Kind = (StatKind)row.Kind,
            Baseline = row.Baseline,
            Current = row.Current,
            Unit = row.Unit,
            Score = row.Score <= 0 ? 1 : row.Score
        };
    }
}
