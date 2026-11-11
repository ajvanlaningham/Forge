using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// Kind of a <see cref="QuestCompletionRow"/>. The daily quests are retired (Decision 20); rows
    /// of kind Mobility now record active rest, and old Strength rows are history only.
    /// </summary>
    public enum QuestKind
    {
        Strength,
        Mobility,
        Conditioning
    }

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

        /// <summary>XP actually granted (base × perfect-week multiplier) — reclaimed exactly on undo.</summary>
        public int XpAwarded { get; set; }
    }
}
