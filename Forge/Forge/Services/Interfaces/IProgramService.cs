using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>The block currently being run.</summary>
    public sealed record ActiveProgram(int BlockId, ProgramDefinition Definition, DateOnly StartedOn);

    /// <summary>Where the block stands on a date (Decisions 12–15).</summary>
    public sealed record BlockView(
        ActiveProgram Program,
        int Week,
        ResolvedDay? Day,
        bool CompletedOnDate,
        bool BlockComplete,
        IReadOnlySet<int> DoneThisWeek,
        int PerfectWeekStreak,
        double Multiplier)
    {
        public int DayCount => Program.Definition.TrainingDaysPerWeek;
        /// <summary>True when every training day of this week is done; the rest of it is active rest.</summary>
        public bool WeekTrainingDone => Day is null;
    }

    /// <summary>
    /// Training programs as 4-week blocks: the catalog shipped in the app package, the active
    /// block, which training day is up, and the perfect-week multiplier. The training day <i>is</i>
    /// the Strength quest while a program is active; active rest replaces the mobility quest.
    /// </summary>
    public interface IProgramService
    {
        Task<IReadOnlyList<ProgramDefinition>> GetProgramsAsync();
        Task<ProgramDefinition?> GetProgramAsync(string id, int version);

        /// <summary>The active block. On a fresh install the default program starts automatically.</summary>
        Task<ActiveProgram?> GetActiveAsync();

        /// <summary>Ends the current block and starts <paramref name="programId"/> at week 1, today.</summary>
        Task<ActiveProgram> StartProgramAsync(string programId, DateOnly today);

        /// <summary>The block's state for <paramref name="date"/>, or null with no active program.</summary>
        Task<BlockView?> GetViewAsync(DateOnly date);

        /// <summary>Marks the training day offered for <paramref name="date"/> complete. No-op if none is offered or it's done.</summary>
        Task CompleteDayAsync(DateOnly date, int? sessionId = null);

        /// <summary>Un-completes the training day done on <paramref name="date"/>. XP is reclaimed by <see cref="ReconcileXpAsync"/>.</summary>
        Task UncompleteDayAsync(DateOnly date);

        /// <summary>Grants (× multiplier) or reclaims (exactly) training-day XP for <paramref name="date"/>. Returns the net delta.</summary>
        Task<int> ReconcileXpAsync(DateOnly date);

        /// <summary>The XP multiplier in force on a date: ×1.2 while the perfect-week streak is alive.</summary>
        Task<double> MultiplierForAsync(DateOnly date);

        /// <summary>The training day a session completed and the XP it earned, or null.</summary>
        Task<(int DayIndex, int Xp)?> DayCompletedBySessionAsync(int sessionId);

        /// <summary>Training days completed in a block so far (the forge meter, Decision 18).</summary>
        Task<int> CompletedDaysInBlockAsync(int blockId);
    }
}
