namespace Forge.Services.Interfaces
{
    /// <summary>
    /// The one place XP changes. Every award (training days, active rest, conditioning weeks,
    /// weigh-ins) decides its amount with <see cref="Forge.Constants.XpLedger"/> and applies it here,
    /// one change at a time, so two awards landing together can't overwrite each other.
    /// </summary>
    public interface IXpService
    {
        Task<int> GetTotalAsync();

        /// <summary>Adds (or, negative, takes back) XP. The total never goes below zero. Returns the new total.</summary>
        Task<int> ApplyAsync(int delta);
    }
}
