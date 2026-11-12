using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// One day's recovery numbers (PBI 3.4): resting heart rate, sleep and steps. Each field
    /// records where it came from (<see cref="Forge.Constants.DataSource"/>), so a sync never
    /// overwrites what was typed by hand.
    /// </summary>
    [Table("DailyCheckIn")]
    public sealed class DailyCheckInRow
    {
        /// <summary>yyyy-MM-dd.</summary>
        [PrimaryKey]
        public string DateKey { get; set; } = "";

        public int? RestingHr { get; set; }
        public int RestingHrSource { get; set; }

        public int? SleepMinutes { get; set; }
        public int SleepSource { get; set; }

        public int? Steps { get; set; }
        public int StepsSource { get; set; }

        /// <summary>UTC ticks of the last Health Connect / HealthKit read for this day.</summary>
        public long? SyncedAtUtc { get; set; }
    }
}
