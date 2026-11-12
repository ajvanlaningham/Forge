using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// A guild bonus as paid to this player (Decision 24): "week:2026-11-09", "hearth:2026-11-12",
    /// "banner:1". Paid and reclaimed through <c>XpLedger</c> like every other award.
    /// </summary>
    [Table("GuildReward")]
    public sealed class GuildRewardRow
    {
        [PrimaryKey]
        public string Key { get; set; } = "";

        public bool Granted { get; set; }
        public int Awarded { get; set; }
    }
}
