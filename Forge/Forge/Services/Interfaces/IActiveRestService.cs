namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Active rest (Decision 12): the light movement done on days without training. It keeps the
    /// daily streak and the perfect-week streak alive and earns a small XP award. Stored as a
    /// <see cref="Forge.Models.QuestCompletionRow"/> of kind Mobility, which is what it was when it
    /// was the daily Mobility quest (retired by Decision 20).
    /// </summary>
    public interface IActiveRestService
    {
        Task<bool> IsDoneAsync(DateOnly date);

        /// <summary>
        /// Marks active rest done or not done and reconciles XP (granting it, or reclaiming exactly
        /// what was granted). Training-day XP is reconciled too, so the net figure is complete.
        /// </summary>
        /// <returns>Net XP change.</returns>
        Task<int> SetDoneAsync(DateOnly date, bool done);
    }
}
