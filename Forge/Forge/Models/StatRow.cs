using SQLite;

namespace Forge.Models
{
    [Table("Stat")]
    public sealed class StatRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed("IX_UserStat_Kind", 1)]
        public string UserStatsId { get; set; } = default!;

        [Indexed("IX_UserStat_Kind", 2), NotNull]
        public int Kind { get; set; }

        public double Baseline { get; set; }
        public double Current { get; set; }
        public string Unit { get; set; } = "";
        public int Score { get; set; } = 1;
    }
}
