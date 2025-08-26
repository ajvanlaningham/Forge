using SQLite;

namespace Forge.Models
{
    [Table("QuestCompletion")]
    public sealed class QuestCompletionRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Stored as yyyy-MM-dd to keep things human/readable & easy to query/group
        [Indexed("IX_QuestCompletion_DateKind", 0), NotNull]
        public string DateKey { get; set; } = "";

        [Indexed("IX_QuestCompletion_DateKind", 1), NotNull]
        public int Kind { get; set; } // QuestKind as int

        // Whether user marked this quest complete
        public bool Completed { get; set; }

        // Whether +50 XP has been granted for this quest today
        public bool XpGranted { get; set; }
    }
}
