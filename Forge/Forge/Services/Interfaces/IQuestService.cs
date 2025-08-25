using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Orchestrates generation and retrieval of daily quests (STR / DEX / CON).
    /// This first pass intentionally avoids returning a concrete DailyQuest model
    /// so we can add that in the next step without breaking the build.
    /// </summary>
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

        Task<bool> AreAllQuestsCompletedAsync(DateOnly date, CancellationToken ct = default);

        Task<int> TryAwardDailyCompletionXpAsync(DateOnly date, CancellationToken ct = default);

    }
}