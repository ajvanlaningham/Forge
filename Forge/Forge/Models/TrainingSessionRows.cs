using SQLite;

namespace Forge.Models
{
    public enum SessionStatus
    {
        InProgress = 0,
        Finished = 1,
        Discarded = 2
    }

    /// <summary>
    /// One workout. Everything about an in-progress session is written to SQLite as it happens,
    /// so it survives the app being backgrounded or killed and resumes from the Train tab.
    /// </summary>
    [Table("TrainingSession")]
    public sealed class TrainingSessionRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>yyyy-MM-dd of the day the session started.</summary>
        [Indexed, NotNull]
        public string DateKey { get; set; } = "";

        /// <summary>UTC ticks.</summary>
        public long StartedAtUtc { get; set; }
        public long? EndedAtUtc { get; set; }

        [Indexed]
        public int Status { get; set; } // SessionStatus

        public int Location { get; set; } // TrainingLocation

        // Program context, when started from a program day. Null for an empty session.
        public string? ProgramId { get; set; }
        public int? ProgramVersion { get; set; }
        public int? BlockId { get; set; }
        public int? DayIndex { get; set; }
        public string? Title { get; set; }

        public string? Notes { get; set; }

        /// <summary>UTC ticks when the current rest countdown ends (PBI 8.3). Null when not resting.</summary>
        public long? RestEndsAtUtc { get; set; }

        /// <summary>Seconds left when the rest was paused. Null when not paused.</summary>
        public int? RestPausedSeconds { get; set; }
    }

    /// <summary>An exercise within a session, with what was prescribed for it.</summary>
    [Table("SessionExercise")]
    public sealed class SessionExerciseRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int SessionId { get; set; }

        /// <summary>Display order within the session (reorderable).</summary>
        public int Position { get; set; }

        /// <summary>Exercise actually performed (library name — the stable key).</summary>
        [Indexed, NotNull]
        public string ExerciseName { get; set; } = "";

        /// <summary>The program's exercise when this one was swapped in for it.</summary>
        public string? SwappedFrom { get; set; }

        /// <summary>Program slot index, or null for an exercise added ad hoc.</summary>
        public int? SlotIndex { get; set; }

        // Prescription (copied from the program slot so history keeps it).
        public int? TargetSets { get; set; }
        public int? RepsMin { get; set; }
        public int? RepsMax { get; set; }
        public int? TargetSeconds { get; set; }
        public int? RestSeconds { get; set; }

        public bool Skipped { get; set; }
    }

    /// <summary>A logged set. Loads are stored in pounds, like body weight.</summary>
    [Table("SessionSet")]
    public sealed class SessionSetRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int SessionExerciseId { get; set; }

        [Indexed]
        public int SessionId { get; set; }

        public int Position { get; set; }

        /// <summary>Pounds. Dumbbells: one handle. Kettlebells: one bell. 0 for bodyweight.</summary>
        public double LoadLb { get; set; }

        public int? Reps { get; set; }
        public int? Seconds { get; set; }

        /// <summary>Warm-ups never count toward progression, volume, or PRs.</summary>
        public bool IsWarmup { get; set; }

        public long LoggedAtUtc { get; set; }
    }
}
