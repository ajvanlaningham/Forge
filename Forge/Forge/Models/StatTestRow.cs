using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// One stat test (protocol v2, Decision 23): the raw measures, the bodyweight at the time, and
    /// the score it earned. A calibration run (week 1 of the first quest) is kept but never scored.
    /// </summary>
    [Table("StatTest")]
    public sealed class StatTestRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>StatKind as int.</summary>
        [Indexed]
        public int Kind { get; set; }

        public int Protocol { get; set; } = 2;

        /// <summary>yyyy-MM-dd.</summary>
        public string DateKey { get; set; } = "";

        /// <summary>The block it was taken in, if any.</summary>
        public int? BlockId { get; set; }

        /// <summary>A practice run: recorded for reference, not scored, never a baseline.</summary>
        public bool Calibration { get; set; }

        /// <summary>Scorable measures, key → value (e.g. "lift:Goblet Squat" → e1RM/bodyweight).</summary>
        public string MeasuresJson { get; set; } = "{}";

        /// <summary>The raw inputs for display (load × reps, cm per side, pulse count…).</summary>
        public string DetailJson { get; set; } = "{}";

        public double BodyweightLb { get; set; }

        /// <summary>0 for a calibration run.</summary>
        public int Score { get; set; }
    }
}
