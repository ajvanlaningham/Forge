using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Orchestrates generation, retrieval, and completion of daily quests.
    /// </summary>
    /// <remarks>
    /// Strength and Mobility are daily. Conditioning is tracked weekly by
    /// <see cref="IConditioningWeekService"/>, so it is deliberately absent from the
    /// daily completion checks here.
    /// </remarks>
    public interface IQuestService
    {
        /// <summary>One-time setup (e.g., ensure repositories/tables are ready).</summary>
        Task InitializeAsync();

        /// <summary>Checks if a daily quest exists for the given date.</summary>
        Task<bool> HasDailyQuestAsync(DateOnly date, CancellationToken ct = default);

        Task<Forge.Models.DailyQuests> GetDailyQuestsAsync(
            DateOnly date,
            CancellationToken ct = default);

        /// <summary>Generates (or regenerates) the daily quest for the given date.</summary>
        Task GenerateDailyQuestAsync(DateOnly date, CancellationToken ct = default);

        Task CompleteQuestAsync(DateOnly date, Forge.Models.QuestKind kind, CancellationToken ct = default);

        Task<bool> IsQuestCompletedAsync(DateOnly date, Forge.Models.QuestKind kind, CancellationToken ct = default);

        Task UncompleteQuestAsync(DateOnly date, Forge.Models.QuestKind kind, CancellationToken ct = default);

        /// <summary>
        /// True when both daily quests (Strength and Mobility) are complete for the date.
        /// Conditioning is weekly and is not considered here.
        /// </summary>
        /// <remarks>Currently has no callers; retained as the natural hook for a daily-streak bonus.</remarks>
        Task<bool> AreCoreQuestsCompletedAsync(DateOnly date, CancellationToken ct = default);

        /// <summary>
        /// Reconciles XP against completion state for each daily quest, granting XP for newly
        /// completed quests and reclaiming it for ones that were un-completed.
        /// </summary>
        /// <returns>Net XP delta: a multiple of the per-quest award, positive, negative, or zero.</returns>
        Task<int> TryAwardQuestXpAsync(DateOnly date, CancellationToken ct = default);

    }
}