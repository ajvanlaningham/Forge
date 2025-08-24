using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Resources.Strings;
using Forge.Constants;

namespace Forge.ViewModels.SubPages
{
    public sealed class MyGearViewModel : BaseViewModel
    {
        private readonly IInventoryService _inventory;

        public ObservableCollection<GearItem> Gear { get; } = new();
        public ObservableCollection<GearGroup> GroupedGear { get; } = new();

        public ICommand ToggleOwnedCommand { get; }
        public ICommand RefreshCommand { get; }

        public MyGearViewModel(IInventoryService inventory)
        {
            _inventory = inventory;

            Title = AppResources.GearPage_Title;
            ToggleOwnedCommand = new Command<GearItem>(async item => await ToggleOwnedAsync(item)); 
            RefreshCommand = new AsyncRelayCommand(LoadAsync);

        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;

                Gear.Clear();
                GroupedGear.Clear();

                // Get what the user currently owns
                var ownedFlags = await _inventory.GetOwnedFlagsAsync();

                // Enumerate all Equipment values (except None)
                foreach (var e in Enum.GetValues(typeof(Equipment)).Cast<Equipment>())
                {
                    if (e == Equipment.None) continue;

                    var owned = (ownedFlags & e) == e;

                    Gear.Add(new GearItem
                    {
                        Equipment = e,
                        Name = e.ToString(),
                        Owned = owned
                    });
                }


                var order = UiConstants.EquipmentGroupOrder;
                var byGroup = Gear
                    .GroupBy(i => EquipmentGroups.GetGroup(i.Equipment))
                    .OrderBy(g => Array.IndexOf(order, g.Key) is int idx && idx >= 0 ? idx : int.MaxValue)
                    .ThenBy(g => g.Key);

                foreach (var g in byGroup)
                {
                    var group = new GearGroup(g.Key);
                    foreach (var item in g.OrderBy(i => i.Name))
                        group.Add(item);
                    GroupedGear.Add(group);
                }
            }
            finally { IsBusy = false; }
        }

        private async Task ToggleOwnedAsync(GearItem? item)
        {
            if (item is null) return;

            var newOwned = !item.Owned;
            await _inventory.SetOwnedAsync(item.Equipment, newOwned);
            item.Owned = newOwned; // reflect immediately
        }

    }
}
