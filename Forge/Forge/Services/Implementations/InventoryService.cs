using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class InventoryService : IInventoryService
    {
        private readonly IRepository<UserInventory> _inventoryRepo;
        private bool _initialized;

        public InventoryService(IRepository<UserInventory> inventoryRepo)
        {
            _inventoryRepo = inventoryRepo;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            // Ensures the UserInventory table exists (sqlite-net will create if missing)
            await _inventoryRepo.GetAllAsync();
            _initialized = true;
        }

        public async Task<IReadOnlyList<UserInventory>> GetAllAsync()
        {
            await InitializeAsync();
            var list = await _inventoryRepo.GetAllAsync();
            return list.OrderBy(r => r.Equipment.ToString()).ToList();
        }

        public async Task<Equipment> GetOwnedFlagsAsync()
        {
            await InitializeAsync();
            var all = await _inventoryRepo.GetAllAsync();
            Equipment flags = Equipment.None;
            foreach (var row in all)
            {
                if (row.Owned)
                    flags |= row.Equipment;
            }
            return flags;
        }

        public async Task SetOwnedAsync(Equipment equipment, bool owned)
        {
            await InitializeAsync();

            // Find existing row for this equipment (unique by Equipment)
            var existing = (await _inventoryRepo.WhereAsync(r => r.Equipment == equipment)).FirstOrDefault();

            if (existing is null)
            {
                var row = new UserInventory
                {
                    Equipment = equipment,
                    Owned = owned
                };
                await _inventoryRepo.InsertAsync(row);
            }
            else
            {
                if (existing.Owned == owned) return; // No-op
                existing.Owned = owned;
                await _inventoryRepo.UpdateAsync(existing);
            }
        }

        public async Task SetOwnedAsync(IEnumerable<Equipment> equipmentOwned)
        {
            await InitializeAsync();

            var ownedSet = new HashSet<Equipment>(equipmentOwned);

            // Pull current rows
            var all = await _inventoryRepo.GetAllAsync();

            // Upsert owned=true for provided set
            foreach (var e in ownedSet)
            {
                var row = all.FirstOrDefault(r => r.Equipment == e);
                if (row is null)
                {
                    await _inventoryRepo.InsertAsync(new UserInventory { Equipment = e, Owned = true });
                }
                else if (!row.Owned)
                {
                    row.Owned = true;
                    await _inventoryRepo.UpdateAsync(row);
                }
            }
        }

        public async Task SetFromFlagsAsync(Equipment ownedFlags)
        {
            await InitializeAsync();

            // Enumerate all enum values except None
            var allValues = Enum.GetValues(typeof(Equipment)).Cast<Equipment>()
                                .Where(e => e != Equipment.None);

            var current = await _inventoryRepo.GetAllAsync();
            var byKey = current.ToDictionary(r => r.Equipment, r => r);

            foreach (var e in allValues)
            {
                bool shouldOwn = (ownedFlags & e) == e;

                if (!byKey.TryGetValue(e, out var row))
                {
                    // Only create a row if owned; otherwise skip until user toggles it
                    if (shouldOwn)
                    {
                        await _inventoryRepo.InsertAsync(new UserInventory { Equipment = e, Owned = true });
                    }
                }
                else if (row.Owned != shouldOwn)
                {
                    row.Owned = shouldOwn;
                    await _inventoryRepo.UpdateAsync(row);
                }
            }
        }
    }
}
