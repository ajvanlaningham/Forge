using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>The program currently being run.</summary>
    public sealed record ActiveProgram(int BlockId, ProgramDefinition Definition, DateOnly StartedOn);

    /// <summary>The program day offered for a date, and whether it was already done that day.</summary>
    public sealed record ProgramDayView(ActiveProgram Program, int DayIndex, ProgramDay Day, bool CompletedOnDate)
    {
        public int DayNumber => DayIndex + 1;
        public int DayCount => Program.Definition.Days.Count;
    }

    /// <summary>
    /// Training programs: the catalog shipped in the app package, the active block, and which
    /// program day is up. The program day <i>is</i> the Strength quest while a program is active.
    /// </summary>
    public interface IProgramService
    {
        /// <summary>Latest version of every shipped program.</summary>
        Task<IReadOnlyList<ProgramDefinition>> GetProgramsAsync();

        /// <summary>A specific program version (old versions stay loadable for history).</summary>
        Task<ProgramDefinition?> GetProgramAsync(string id, int version);

        /// <summary>
        /// The active program. On first use, the default program is started automatically so a
        /// fresh install has something to train.
        /// </summary>
        Task<ActiveProgram?> GetActiveAsync();

        /// <summary>Ends the current block and starts the latest version of <paramref name="programId"/> at Day 1.</summary>
        Task<ActiveProgram> StartProgramAsync(string programId, DateOnly today);

        /// <summary>The day offered for <paramref name="date"/>, or null with no active program.</summary>
        Task<ProgramDayView?> GetDayForDateAsync(DateOnly date);

        /// <summary>Marks the day offered for <paramref name="date"/> complete. No-op if already done that date.</summary>
        Task CompleteDayAsync(DateOnly date, int? sessionId = null);

        /// <summary>Un-completes the day done on <paramref name="date"/>. XP is reclaimed by <see cref="ReconcileXpAsync"/>.</summary>
        Task UncompleteDayAsync(DateOnly date);

        /// <summary>
        /// Grants XP for program days newly completed on <paramref name="date"/> and reclaims it for
        /// ones un-completed. Returns the net delta (a multiple of the per-day award).
        /// </summary>
        Task<int> ReconcileXpAsync(DateOnly date);
    }
}
