using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services.Interfaces;

namespace Forge.ViewModels.SubPages
{
    public sealed class ExerciseLibraryViewModel : BaseViewModel
    {
        private readonly IExerciseLibraryService _lib;
        private readonly IExerciseLibraryImporter _importer;
        private readonly IInventoryService _inventory;

        public ExerciseLibraryViewModel(IExerciseLibraryService lib, IExerciseLibraryImporter importer, IInventoryService inventoryService)
        {
            _lib = lib;
            _importer = importer;
            _inventory = inventoryService;

            Title = AppResources.ExerciseLibrary_Title;
            Items = new ObservableCollection<Exercise>();

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        }

        public ObservableCollection<Exercise> Items { get; }

        // Simple filters bound to Pickers/SearchBar
        private ExerciseCategory? _category;
        public ExerciseCategory? Category { get => _category; set { if (SetProperty(ref _category, value)) _ = LoadAsync(); } }

        private BodyZone? _bodyZone;
        public BodyZone? BodyZone { get => _bodyZone; set { if (SetProperty(ref _bodyZone, value)) _ = LoadAsync(); } }

        private string _searchText = string.Empty;
        public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        private static bool IsAllowedByInventory(Exercise e, Equipment owned)
        {
            // Always allow if no gear is required
            if (e.Equipment == Equipment.None) return true;
            // Require user to own ALL flags the exercise needs
            return (e.Equipment & owned) == e.Equipment;
        }

        private async Task<Equipment?> GetOwnedFlagsOrNullAsync()
        {
            //If the user hasnt set any inventory yet, return null to avoid hiding all exercises
            var rows = await _inventory.GetAllAsync();
            if (rows.Count == 0) return null;
            return await _inventory.GetOwnedFlagsAsync();
        }

        private async Task<List<Exercise>> ApplyInventoryFilterAsync(IEnumerable<Exercise> source)
        {
            var ownedFlags = await GetOwnedFlagsOrNullAsync();
            if (!ownedFlags.HasValue)
                return source.ToList(); // no inventory set yet → don't hide anything

            return source.Where(e => IsAllowedByInventory(e, ownedFlags.Value)).ToList();
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                // Make sure library exists & table ensured
                await _importer.EnsureSeededAsync();
                await _lib.InitializeAsync();


                var list = await _lib.FilterAsync(
                    category: Category,
                    bodyZone: BodyZone,
                    requiresAllEquipmentFlags: null,
                    maxSkill: null,
                    onlyActive: true);

                //inventory aware filtering
                list = await ApplyInventoryFilterAsync(list);

                UpdateItems(list);
            }
            finally { IsBusy = false; }
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadAsync();
                return;
            }

            var results = await _lib.SearchAsync(SearchText, onlyActive: true);
            // Also apply pickers on top of search results
            results = results.Where(e =>
                (!Category.HasValue || e.Category == Category) &&
                (!BodyZone.HasValue || e.BodyZone == BodyZone)
            ).ToList();

            results = await ApplyInventoryFilterAsync(results);

            UpdateItems(results);
        }

        private async Task ClearFiltersAsync()
        {
            Category = null;
            BodyZone = null;
            SearchText = string.Empty;
            await LoadAsync();
        }

        private void UpdateItems(IReadOnlyList<Exercise> list)
        {
            Items.Clear();
            foreach (var e in list.OrderBy(e => e.Name)) Items.Add(e);
        }
    }
}