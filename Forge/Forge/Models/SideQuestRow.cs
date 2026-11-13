using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// A side quest (user, 2026-11-13): training done your own way, one per day. It stands in for
    /// the day's training (a <c>ProgramDayCompletionRow</c> "via" this activity) or its active rest.
    /// </summary>
    [Table("SideQuest")]
    public sealed class SideQuestRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed, NotNull]
        public string DateKey { get; set; } = "";

        /// <summary>"HEMA", "Gym class", "Yoga"…</summary>
        public string Activity { get; set; } = "";

        public int Minutes { get; set; }

        /// <summary>An <c>Effort</c> as int.</summary>
        public int Effort { get; set; }

        /// <summary>A <c>SideQuestCounts</c> as int: what it actually counted as.</summary>
        public int CountsAs { get; set; }

        public string? Note { get; set; }

        public long LoggedAtUnix { get; set; }
    }
}
