using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// One run of one program. Starting a different program (or restarting this one) ends the
    /// current block and opens a new one, so the day rotation starts fresh at Day 1.
    /// </summary>
    [Table("ProgramBlock")]
    public sealed class ProgramBlockRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string ProgramId { get; set; } = "";

        public int ProgramVersion { get; set; }

        /// <summary>yyyy-MM-dd.</summary>
        [NotNull]
        public string StartedDateKey { get; set; } = "";

        /// <summary>yyyy-MM-dd, or null while this is the active block.</summary>
        public string? EndedDateKey { get; set; }
    }

    /// <summary>
    /// A program day done within a block. Keyed by program day <b>and</b> the date it was done:
    /// a Day 2 performed on a Thursday is still Day 2.
    /// </summary>
    /// <remarks>
    /// Rows are kept (Completed = false) after an un-complete until the XP reconcile reclaims the
    /// award, mirroring <see cref="QuestCompletionRow"/>, so XP stays symmetric.
    /// </remarks>
    [Table("ProgramDayCompletion")]
    public sealed class ProgramDayCompletionRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed("IX_ProgramDay_BlockDate", 0)]
        public int BlockId { get; set; }

        [Indexed("IX_ProgramDay_BlockDate", 1), NotNull]
        public string DateKey { get; set; } = "";

        public int DayIndex { get; set; }

        public bool Completed { get; set; }

        public bool XpGranted { get; set; }

        /// <summary>The training session that completed this day, if it was logged (PBI 8.2).</summary>
        public int? SessionId { get; set; }

        /// <summary>XP actually granted (base × perfect-week multiplier) — reclaimed exactly on undo.</summary>
        public int XpAwarded { get; set; }
    }
}
