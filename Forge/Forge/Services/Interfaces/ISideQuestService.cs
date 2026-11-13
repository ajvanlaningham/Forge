using Forge.Models;
using Forge.Training;

namespace Forge.Services.Interfaces
{
    /// <summary>An activity you've logged before, with how it usually goes.</summary>
    public sealed record SideQuestActivity(string Name, int Minutes, Effort Effort);

    /// <summary>Side quests (user, 2026-11-13): training done your own way, so it counts.</summary>
    public interface ISideQuestService
    {
        Task<SideQuestRow?> GetAsync(DateOnly date);

        /// <summary>Your activities, most recent first, each with its last length and effort.</summary>
        Task<IReadOnlyList<SideQuestActivity>> RecentAsync();

        /// <summary>Whether a side quest logged on <paramref name="date"/> could fill a training day.</summary>
        Task<bool> CanStandInAsync(DateOnly date);

        /// <summary>
        /// Logs (or replaces) the day's side quest. A training one fills today's training day if it
        /// can, else it counts as rest. Returns the net XP change.
        /// </summary>
        Task<int> LogAsync(DateOnly date, string activity, int minutes, Effort effort, SideQuestCounts countsAs, string? note);

        /// <summary>Removes the day's side quest and what it counted for. Returns the net XP change.</summary>
        Task<int> UndoAsync(DateOnly date);
    }
}
