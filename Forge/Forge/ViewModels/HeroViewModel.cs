using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.ViewModels.Controls.Cards;
using Forge.Views.SubPages;

namespace Forge.ViewModels
{
    /// <summary>
    /// The Hero tab (Decision 20): the character. Level and stats, the Armoury (forged items), the
    /// Pack (real equipment) and, later, condition (injuries) and skins.
    /// </summary>
    public sealed class HeroViewModel : BaseViewModel
    {
        private readonly IStatsService _stats;
        private readonly IProgramService _programs;
        private readonly IInventoryService _inventory;
        private readonly ProgressService _progress;

        public StatCardViewModel StatsCard { get; } = new();
        public ObservableCollection<GearItem> Pack { get; } = new();

        private string _armouryText = "";
        public string ArmouryText { get => _armouryText; private set => SetProperty(ref _armouryText, value); }

        private string _streakText = "";
        public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

        private bool _packEmpty;
        public bool PackEmpty { get => _packEmpty; private set => SetProperty(ref _packEmpty, value); }

        public ICommand EditPackCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(MyGearPage)));

        public HeroViewModel(IStatsService stats, IProgramService programs, IInventoryService inventory, ProgressService progress)
        {
            _stats = stats;
            _programs = programs;
            _inventory = inventory;
            _progress = progress;
            Title = "Hero";
        }

        public async Task RefreshAsync()
        {
            StatsCard.Title = "Level & stats";
            await _stats.InitAsync();
            var core = await _stats.GetCoreStatsAsyncFromDb();
            StatsCard.ApplyXp((await _stats.GetUserStatsAsync()).Xp);
            StatsCard.Strength = core.FirstOrDefault(s => s.Kind == StatKind.Strength)?.Score ?? 1;
            StatsCard.Dexterity = core.FirstOrDefault(s => s.Kind == StatKind.Dexterity)?.Score ?? 1;
            StatsCard.Constitution = core.FirstOrDefault(s => s.Kind == StatKind.Constitution)?.Score ?? 1;

            var (current, longest) = await _progress.GetStreakAsync();
            StreakText = $"Moved {current} day{(current == 1 ? "" : "s")} in a row · best {Math.Max(current, longest)}";

            // Forging isn't built yet (TODO: Forging & the armoury): show what's in the fire.
            var view = await _programs.GetViewAsync(DateOnly.FromDateTime(DateTime.Today));
            if (view is null)
                ArmouryText = "Empty racks. Accept a quest to forge your first item.";
            else
            {
                var def = view.Program.Definition;
                var done = await _programs.CompletedDaysInBlockAsync(view.Program.BlockId);
                ArmouryText = $"Empty racks, for now.\nIn the fire: the {def.QuestName} · {done} of {Forging.Needed(def.TrainingDaysPerWeek)} training days needed.";
            }

            await _inventory.InitializeAsync();
            var owned = await _inventory.GetOwnedFlagsAsync();
            var gear = UserSettings.HomeGear;
            Pack.Clear();
            foreach (var e in Enum.GetValues<Equipment>().Where(e => e != Equipment.None && owned.HasFlag(e)))
                Pack.Add(new GearItem { Equipment = e, Name = e.ToString(), Owned = true, Summary = gear.Summary(e) });
            PackEmpty = Pack.Count == 0;
        }
    }
}
