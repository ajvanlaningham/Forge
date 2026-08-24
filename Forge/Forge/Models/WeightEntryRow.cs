using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// One logged bodyweight for one day. Storage is always in pounds; the UI toggle in
    /// <see cref="Forge.Services.UserSettings"/> only affects display.
    /// </summary>
    /// <remarks>
    /// <see cref="DateKey"/> is the primary key (yyyy-MM-dd), so there is at most one entry
    /// per calendar day. Re-logging the same day overwrites the previous value via
    /// <c>InsertOrReplace</c>.
    /// </remarks>
    [Table("WeightEntry")]
    public sealed class WeightEntryRow
    {
        [PrimaryKey]
        public string DateKey { get; set; } = "";

        /// <summary>Stored in pounds, always. Convert at the display boundary.</summary>
        public double WeightPounds { get; set; }

        /// <summary>Optional free-text note; null-safe.</summary>
        public string? Note { get; set; }

        /// <summary>Unix seconds of insertion — useful for tie-breaking on "latest" reads.</summary>
        public long LoggedAtUnix { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
