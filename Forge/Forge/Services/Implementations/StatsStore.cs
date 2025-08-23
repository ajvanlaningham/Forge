using SQLite;
using Forge.Services.Interfaces;
using Forge.Models;
using Forge.Contstants;

namespace Forge.Services.Implementations
{
    public sealed class StatsStore : IStatsStore
    {
        private readonly SQLiteAsyncConnection _db;

        public StatsStore()
        {
            SQLitePCL.Batteries_V2.Init();
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, GameConstants.Db.FileName);
            _db = new SQLiteAsyncConnection(dbPath);
        }

        public async Task InitAsync()
        {
            await _db.CreateTableAsync<UserStats>();
            await _db.CreateTableAsync<StatRow>();

            // Seed single UserStats
            var userStats = await _db.Table<UserStats>().FirstOrDefaultAsync();
            if (userStats is null)
            {
                userStats = new UserStats();
                await _db.InsertAsync(userStats);
            }

            // Seed STR/DEX/CON if empty — Score = 1
            var anyStat = await _db.Table<StatRow>().FirstOrDefaultAsync();
            if (anyStat is null)
            {
                var rows = new[]
                {
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Strength,     Baseline = 0, Current = 0, Unit = "×BW",     Score = 1 },
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Dexterity,    Baseline = 0, Current = 0, Unit = "s",       Score = 1 },
                    new StatRow { UserStatsId = userStats.Id, Kind = (int)StatKind.Constitution, Baseline = 0, Current = 0, Unit = "km/10min",Score = 1 },
                };
                await _db.InsertAllAsync(rows);
            }
        }

        public Task<UserStats> GetUserStatsAsync()
            => _db.Table<UserStats>().FirstAsync();

        public async Task UpsertUserStatsAsync(UserStats stats)
        {
            stats.UpdatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            stats.IsDirty = true;
            await _db.InsertOrReplaceAsync(stats);
        }

        // ---- Stats ----
        public async Task<IReadOnlyList<Stat>> GetStatsAsync()
        {
            var rows = await _db.Table<StatRow>().ToListAsync();
            return rows.Select(ToDomain).ToList();
        }

        public async Task<Stat?> GetStatAsync(StatKind kind)
        {
            var row = await _db.Table<StatRow>().Where(r => r.Kind == (int)kind).FirstOrDefaultAsync();
            return row is null ? null : ToDomain(row);
        }

        public async Task UpsertStatAsync(Stat stat)
        {
            var user = await GetUserStatsAsync();
            var existing = await _db.Table<StatRow>().Where(r => r.Kind == (int)stat.Kind).FirstOrDefaultAsync();

            if (existing is null)
            {
                await _db.InsertAsync(new StatRow
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
                await _db.UpdateAsync(existing);
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
