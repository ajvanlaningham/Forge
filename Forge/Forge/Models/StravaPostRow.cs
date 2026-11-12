using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// A session Forge has dealt with on Strava: posted (ActivityId > 0) or refused for good
    /// (ActivityId 0, with the reason), so the queue never retries it forever.
    /// </summary>
    [Table("StravaPost")]
    public sealed class StravaPostRow
    {
        [PrimaryKey]
        public int SessionId { get; set; }

        public long ActivityId { get; set; }

        public long PostedAtUtc { get; set; }

        public string? Error { get; set; }
    }
}
