using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IInventoryService
    {
        /// <summary>Ensures the underlying table exists.</summary>
        Task InitializeAsync();

        /// <summary>Returns the set of equipment rows (Owned/Not).</summary>
        Task<IReadOnlyList<UserInventory>> GetAllAsync();

        /// <summary>Returns a bitwise flags value of all equipment the user owns.</summary>
        Task<Equipment> GetOwnedFlagsAsync();

        /// <summary>Mark a single equipment item owned/unowned (upsert).</summary>
        Task SetOwnedAsync(Equipment equipment, bool owned);

        /// <summary>Convenience: set multiple items owned (upsert true for each).</summary>
        Task SetOwnedAsync(IEnumerable<Equipment> equipmentOwned);

        /// <summary>Replace inventory from a single flags value (idempotent).</summary>
        Task SetFromFlagsAsync(Equipment ownedFlags);
    }
}