using Forge.Models;
using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// The end of a quest (Decisions 18, 25): when a block's four weeks are up, judge it once: 75% of
    /// its training days earns one forging, otherwise the metal cools. The player spends a forging on
    /// any of the five slots, raising it one metal (slot × metal, re-skinned to the current hero).
    /// </summary>
    public interface IForgeService
    {
        /// <summary>Judges the active block if its four weeks are up and it hasn't been judged. Returns any result not yet shown.</summary>
        Task<ForgeResultRow?> JudgeIfDueAsync(DateOnly today);

        Task MarkSeenAsync(int resultId);

        /// <summary>The result for a block, once judged.</summary>
        Task<ForgeResultRow?> GetResultAsync(int blockId);

        /// <summary>Every forging spent, oldest first.</summary>
        Task<IReadOnlyList<Forged>> GetForgedAsync();

        /// <summary>Passed blocks whose forging hasn't been spent yet, oldest first.</summary>
        Task<IReadOnlyList<ForgeResultRow>> UnspentAsync();

        /// <summary>
        /// Spends the forging earned by <paramref name="blockId"/> on <paramref name="slot"/>. Returns
        /// the metal reached, or null if there's nothing to spend (not passed, already spent, or the
        /// slot is at Starmetal).
        /// </summary>
        Task<string?> SpendAsync(int blockId, GearSlot slot, string? form = null);
    }
}
