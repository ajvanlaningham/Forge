using Forge.Models;
using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>A session with its exercises and sets, ready to display.</summary>
    public sealed class SessionSnapshot
    {
        public required TrainingSessionRow Session { get; init; }
        public required IReadOnlyList<SessionExerciseSnapshot> Exercises { get; init; }
        public TrainingLocation Location => (TrainingLocation)Session.Location;
    }

    /// <summary>A program session before it starts: the briefing.</summary>
    public sealed class SessionPlan
    {
        public required DateOnly Date { get; init; }
        public required TrainingLocation Location { get; init; }
        public required string Title { get; init; }
        public required ActiveProgram Program { get; init; }
        public required Forge.Training.ResolvedDay Day { get; init; }
        public required IReadOnlyList<PlannedExercise> Exercises { get; init; }
    }

    public sealed class PlannedExercise
    {
        public required string Name { get; init; }
        public string? SwappedFrom { get; init; }
        public required Forge.Training.ProgramSlot Slot { get; init; }
        public required int SlotIndex { get; init; }
        public required bool IsMain { get; init; }
        public ExerciseInfo? Info { get; init; }
        // Settled in the briefing.
        public double PlannedLoadLb { get; set; }
        public int? PlannedReps { get; set; }
        public int? PlannedSeconds { get; set; }
    }

    public sealed class SessionExerciseSnapshot
    {
        public required SessionExerciseRow Row { get; init; }
        public required IReadOnlyList<SessionSetRow> Sets { get; init; }
        public ExerciseInfo? Info { get; init; }
        public bool IsTimed => Row.TargetSeconds is not null
                               || Info?.Action is ActionType.Hold or ActionType.Time;
    }

    /// <summary>One past session's working sets for an exercise.</summary>
    public sealed record ExerciseHistoryEntry(DateOnly Date, int SessionId, string? SessionTitle, IReadOnlyList<SessionSetRow> WorkingSets);

    /// <summary>What was done for an exercise the last time it was performed.</summary>
    public sealed record LastPerformance(DateOnly Date, IReadOnlyList<SessionSetRow> WorkingSets);

    /// <summary>
    /// Training session logging (PBI 8.1). Every change is persisted immediately, so an
    /// in-progress session survives process death.
    /// </summary>
    public interface ITrainingService
    {
        /// <summary>The unfinished session, if any. There is at most one.</summary>
        Task<SessionSnapshot?> GetInProgressAsync();

        Task<SessionSnapshot> GetSessionAsync(int sessionId);

        /// <summary>
        /// Starts a session from the program day offered for <paramref name="date"/>, resolving
        /// each slot against the equipment at <paramref name="location"/> (swapping where needed).
        /// </summary>
        Task<SessionSnapshot> StartFromProgramAsync(DateOnly date, TrainingLocation location);

        /// <summary>
        /// What today's program session would be at <paramref name="location"/>, swaps applied,
        /// without starting anything: the briefing shown before BEGIN.
        /// </summary>
        Task<SessionPlan> PlanFromProgramAsync(DateOnly date, TrainingLocation location);

        /// <summary>Any day of the active quest, as planned at <paramref name="location"/> (swaps applied): the map and its drawer.</summary>
        Task<SessionPlan> PlanForDayAsync(int dayIndex, int week, TrainingLocation location);

        /// <summary>Starts the session a briefing settled on (loads and targets included).</summary>
        Task<SessionSnapshot> StartFromPlanAsync(SessionPlan plan);

        /// <summary>Corrects a logged set (the guided session logs targets; the user fixes misses).</summary>
        Task UpdateSetAsync(int setId, double loadLb, int? reps, int? seconds);

        /// <summary>Starts an empty session for unplanned training.</summary>
        Task<SessionSnapshot> StartEmptyAsync(DateOnly date, TrainingLocation location);

        Task AddExerciseAsync(int sessionId, string exerciseName);
        Task SwapExerciseAsync(int sessionExerciseId, string newExerciseName);
        Task SetSkippedAsync(int sessionExerciseId, bool skipped);
        Task MoveExerciseAsync(int sessionExerciseId, int delta);

        Task<SessionSetRow> LogSetAsync(int sessionExerciseId, double loadLb, int? reps, int? seconds, bool isWarmup);
        Task DeleteSetAsync(int setId);

        /// <summary>Starts (or restarts) the rest countdown. Persisted, so it survives process death.</summary>
        Task<TrainingSessionRow> StartRestAsync(int sessionId, int seconds);

        /// <summary>Adds (or removes, when negative) time to the running or paused rest.</summary>
        Task<TrainingSessionRow> AdjustRestAsync(int sessionId, int deltaSeconds);

        /// <summary>Pauses a running rest or resumes a paused one.</summary>
        Task<TrainingSessionRow> ToggleRestPauseAsync(int sessionId);

        Task<TrainingSessionRow> StopRestAsync(int sessionId);

        /// <summary>Finishes the session. Returns the finished snapshot.</summary>
        Task<SessionSnapshot> FinishAsync(int sessionId);

        /// <summary>Abandons the session. Its sets are kept but excluded from history and progression.</summary>
        Task DiscardAsync(int sessionId);

        /// <summary>Working sets from the most recent finished session containing the exercise.</summary>
        Task<LastPerformance?> GetLastPerformanceAsync(string exerciseName, int? excludingSessionId = null);

        /// <summary>
        /// Working sets for an exercise from recent finished sessions, most recent first — the
        /// input to <see cref="Progression.Suggest"/>. Sessions where it had no working sets are left out.
        /// </summary>
        Task<IReadOnlyList<IReadOnlyList<WorkSet>>> GetRecentWorkingSetsAsync(string exerciseName, int? excludingSessionId = null, int sessions = 4);

        /// <summary>Exercises doable at a location, for substitutes and "add exercise".</summary>
        /// <summary>
        /// A paced move's last weight and suggested reps, with how that workout felt (Decision 28);
        /// null if it has never been done in a finished session.
        /// </summary>
        Task<PacedLast?> GetPacedLastAsync(string exerciseName);

        /// <summary>Records the after-workout Easy / Right / Hard tap (null clears it).</summary>
        Task SetFeelAsync(int sessionId, Feel? feel);

        Task<IReadOnlyList<ExerciseInfo>> GetLibraryAsync(ExerciseCategory? category = null);

        /// <summary>Equipment available at a location.</summary>
        Task<Equipment> GetEquipmentAsync(TrainingLocation location);

        /// <summary>Every finished session's working sets for an exercise, oldest first.</summary>
        Task<IReadOnlyList<ExerciseHistoryEntry>> GetExerciseHistoryAsync(string exerciseName);

        /// <summary>
        /// Personal records set in a finished session, per exercise, measured against every
        /// earlier finished session. Shown neutrally; they earn nothing (see StrengthMath).
        /// </summary>
        Task<IReadOnlyDictionary<string, IReadOnlyList<PersonalRecord>>> GetSessionRecordsAsync(int sessionId);

        /// <summary>Finished sessions, newest first.</summary>
        Task<IReadOnlyList<TrainingSessionRow>> GetHistoryAsync(int limit = 100);
    }
}
