using Forge.Models;
using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// The end of a quest (Decision 18): when a block's four weeks are up, judge it once: 75% of its
    /// training days forges the quest's piece, otherwise the metal cools. Forged pieces fill the
    /// Armoury and open the next tier (Decision 22: slot × metal, re-skinned to the current hero).
    /// </summary>
    public interface IForgeService
    {
        /// <summary>Judges the active block if its four weeks are up and it hasn't been judged. Returns any result not yet shown.</summary>
        Task<ForgeResultRow?> JudgeIfDueAsync(DateOnly today);

        Task MarkSeenAsync(int resultId);

        /// <summary>The result for a block, once judged.</summary>
        Task<ForgeResultRow?> GetResultAsync(int blockId);

        /// <summary>Every forged piece (not the cooled attempts).</summary>
        Task<IReadOnlyList<Forged>> GetForgedAsync();
    }
}
