using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// SQLite row for one logged bodyweight. Storage is always in pounds; the UI toggle in
    /// <see cref="Forge.Services.UserSettings"/> only affects display.
    /// </summary>
    /// <remarks>
    /// <see cref="DateKey"/> is the primary key (yyyy-MM-dd), so there is at most one entry
    /// per calendar day. Re-logging the same day overwrites the previous value via
    /// <c>InsertOrReplace</c>. Only <see cref="Services.Implementations.WeightService"/>
    /// touches this row; callers of <see cref="Services.Interfaces.IWeightService"/> see the
    /// <see cref="WeightEntry"/> domain model.
    /// </remarks>
    [Table("WeightEntry")]
    public sealed class WeightEntryRow
    {
        [PrimaryKey]
        public string DateKey { get; set; } = "";

        /// <summary>Stored in pounds, always. <see cref="decimal"/> so round-tripping through
        /// the DB is lossless — convert to <c>double</c> only at UI/math boundaries.</summary>
        public decimal WeightPounds { get; set; }

        /// <summary>Optional free-text note; null-safe.</summary>
        public string? Note { get; set; }

        /// <summary>Unix seconds of insertion — useful for tie-breaking on "latest" reads.</summary>
        public long LoggedAtUnix { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
