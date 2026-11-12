using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Forge.Services.Implementations
{
    /// <inheritdoc />
    public sealed class DailyCheckInService : IDailyCheckInService
    {
        private readonly IRepository<DailyCheckInRow> _rows;
        private readonly IHealthData _health;
        private readonly ILogger<DailyCheckInService> _log;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public DailyCheckInService(IRepository<DailyCheckInRow> rows, IHealthData health, ILogger<DailyCheckInService> log)
        {
            _rows = rows;
            _health = health;
            _log = log;
        }

        private static string Key(DateOnly d) => d.ToString("yyyy-MM-dd");

        public async Task<DailyCheckInRow> GetAsync(DateOnly date)
            => await _rows.GetByIdAsync(Key(date)) ?? new DailyCheckInRow { DateKey = Key(date) };

        public async Task<DailyCheckInRow> SaveManualAsync(DateOnly date, int? restingHr, int? sleepMinutes, int? steps)
        {
            await _gate.WaitAsync();
            try
            {
                var row = await GetAsync(date);
                (row.RestingHr, row.RestingHrSource) = Manual(row.RestingHr, row.RestingHrSource, restingHr);
                (row.SleepMinutes, row.SleepSource) = Manual(row.SleepMinutes, row.SleepSource, sleepMinutes);
                (row.Steps, row.StepsSource) = Manual(row.Steps, row.StepsSource, steps);
                await _rows.InsertOrReplaceAsync(row);
                return row;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// The form shows synced values too, so saving it unchanged must not turn them into hand
        /// entries: only a value that differs from what's stored becomes Manual.
        /// </summary>
        private static (int?, int) Manual(int? stored, int source, int? typed)
        {
            if (typed is null) return (null, (int)DataSource.None);
            if (typed == stored) return (stored, source);
            return (typed, (int)DataSource.Manual);
        }

        public async Task<int> SyncAsync(params DateOnly[] dates)
        {
            if (_health.Availability != HealthAvailability.Available) return 0;
            if (!await _health.HasAnyPermissionAsync()) return 0;

            await _gate.WaitAsync();
            try
            {
                var changed = 0;
                foreach (var date in dates.Distinct())
                {
                    HealthDay day;
                    try { day = await _health.ReadDayAsync(date); }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "{Store} read failed for {Date}", _health.StoreName, date);
                        continue;
                    }

                    var row = await GetAsync(date);
                    changed += Merge(day.RestingHr, row.RestingHr, row.RestingHrSource, v => { row.RestingHr = v; row.RestingHrSource = (int)DataSource.Synced; });
                    changed += Merge(day.SleepMinutes, row.SleepMinutes, row.SleepSource, v => { row.SleepMinutes = v; row.SleepSource = (int)DataSource.Synced; });
                    changed += Merge(day.Steps, row.Steps, row.StepsSource, v => { row.Steps = v; row.StepsSource = (int)DataSource.Synced; });
                    row.SyncedAtUtc = DateTime.UtcNow.Ticks;
                    await _rows.InsertOrReplaceAsync(row);
                }
                return changed;
            }
            finally { _gate.Release(); }
        }

        private static int Merge(int? synced, int? stored, int source, Action<int> write)
        {
            if (synced is not int v) return 0;
            if (!HealthMath.SyncMayWrite((DataSource)source) || stored == v) return 0;
            write(v);
            return 1;
        }
    }
}
