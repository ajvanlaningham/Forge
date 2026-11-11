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

    public sealed class SessionExerciseSnapshot
    {
        public required SessionExerciseRow Row { get; init; }
        public required IReadOnlyList<SessionSetRow> Sets { get; init; }
        public ExerciseInfo? Info { get; init; }
        public bool IsTimed => Row.TargetSeconds is not null
                               || Info?.Action is ActionType.Hold or ActionType.Time;
    }

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

        /// <summary>Starts an empty session for unplanned training.</summary>
        Task<SessionSnapshot> StartEmptyAsync(DateOnly date, TrainingLocation location);

        Task AddExerciseAsync(int sessionId, string exerciseName);
        Task SwapExerciseAsync(int sessionExerciseId, string newExerciseName);
        Task SetSkippedAsync(int sessionExerciseId, bool skipped);
        Task MoveExerciseAsync(int sessionExerciseId, int delta);

        Task<SessionSetRow> LogSetAsync(int sessionExerciseId, double loadLb, int? reps, int? seconds, bool isWarmup);
        Task DeleteSetAsync(int setId);

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
        Task<IReadOnlyList<ExerciseInfo>> GetLibraryAsync(ExerciseCategory? category = null);

        /// <summary>Equipment available at a location.</summary>
        Task<Equipment> GetEquipmentAsync(TrainingLocation location);

        /// <summary>Finished sessions, newest first.</summary>
        Task<IReadOnlyList<TrainingSessionRow>> GetHistoryAsync(int limit = 100);
    }
}
