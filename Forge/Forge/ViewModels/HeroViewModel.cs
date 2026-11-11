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
    /// <summary>One item on the Armoury rack: lit when forged, faint until then.</summary>
    public sealed record RackItem(string Name, string Sprite, double Opacity, int Column);

    public sealed class HeroViewModel : BaseViewModel
    {
        private readonly IStatsService _stats;
        private readonly IProgramService _programs;
        private readonly IInventoryService _inventory;
        private readonly ProgressService _progress;
        private readonly IForgeService _forge;

        public StatCardViewModel StatsCard { get; } = new();
        public ObservableCollection<GearItem> Pack { get; } = new();

        private string _armouryText = "";
        public string ArmouryText { get => _armouryText; private set => SetProperty(ref _armouryText, value); }

        private string _streakText = "";
        public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

        private bool _packEmpty;
        public bool PackEmpty { get => _packEmpty; private set => SetProperty(ref _packEmpty, value); }

        public ObservableCollection<RackItem> Rack { get; } = new();

        private string _spriteFile = UserSettings.HeroSpriteFile;
        public string SpriteFile { get => _spriteFile; private set => SetProperty(ref _spriteFile, value); }
        private string _heroTitle = UserSettings.HeroTitle;
        public string HeroTitle { get => _heroTitle; private set => SetProperty(ref _heroTitle, value); }
        public ICommand ChangeLookCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(HeroPickerPage)));

        public ICommand EditPackCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(MyGearPage)));

        /// <summary>A test outside its slotted day: still scored (or the baseline, if there isn't one yet).</summary>
        public ICommand TakeTestCommand { get; } = new Command(async () =>
        {
            var pick = await Shell.Current.DisplayActionSheet("Take a stat test", "Cancel", null, "Strength", "Mobility", "Conditioning");
            var kind = pick switch { "Strength" => "Strength", "Mobility" => "Dexterity", "Conditioning" => "Constitution", _ => null };
            if (kind is not null) await Shell.Current.GoToAsync($"{nameof(StatTestPage)}?kind={kind}&calibration=false");
        });

        public HeroViewModel(IStatsService stats, IProgramService programs, IInventoryService inventory, ProgressService progress, IForgeService forge)
        {
            _forge = forge;
            _stats = stats;
            _programs = programs;
            _inventory = inventory;
            _progress = progress;
            Title = "Hero";
        }

        public async Task RefreshAsync()
        {
            SpriteFile = UserSettings.HeroSpriteFile;
            HeroTitle = UserSettings.HeroTitle;
            StatsCard.Title = "Level & stats";
            await _stats.InitAsync();
            var core = await _stats.GetCoreStatsAsyncFromDb();
            StatsCard.ApplyXp((await _stats.GetUserStatsAsync()).Xp);
            StatsCard.Strength = core.FirstOrDefault(s => s.Kind == StatKind.Strength)?.Score ?? 1;
            StatsCard.Dexterity = core.FirstOrDefault(s => s.Kind == StatKind.Dexterity)?.Score ?? 1;
            StatsCard.Constitution = core.FirstOrDefault(s => s.Kind == StatKind.Constitution)?.Score ?? 1;

            var (current, longest) = await _progress.GetStreakAsync();
            StreakText = $"Moved {current} day{(current == 1 ? "" : "s")} in a row · best {Math.Max(current, longest)}";

            var view = await _programs.GetViewAsync(DateOnly.FromDateTime(DateTime.Today));
            // The rack: the open tier's three pieces for this hero (Decision 22). A forged piece shows in
            // the best metal it's been forged in; an unforged one is faint, and the one in the fire a
            // little brighter.
            var forged = await _forge.GetForgedAsync();
            var tier = Armoury.OpenTier(forged);
            var tierMetal = Armoury.Metals[tier];
            var inFire = view is null ? (GearSlot?)null : HeroQuest.Slot(view.Program.Definition);
            Rack.Clear();
            var col = 0;
            foreach (var slot in Armoury.Slots)
            {
                var best = Armoury.BestMetal(forged, slot);
                var metal = best ?? tierMetal;
                var opacity = best is not null ? 1.0 : slot == inFire ? 0.55 : 0.22;
                Rack.Add(new RackItem($"{metal} {HeroQuest.ItemName(slot)}", HeroQuest.Sprite(slot, metal), opacity, col++));
            }
            var doneInTier = Armoury.ForgedIn(forged, tierMetal).Count;
            var next = tier + 1 < Armoury.Metals.Length ? Armoury.Metals[tier + 1] : null;
            var tierLine = next is null
                ? $"{tierMetal} tier: {doneInTier} of 3 forged."
                : $"{tierMetal} tier: {doneInTier} of 3 forged. Forge all three to open {next}.";

            if (view is null)
                ArmouryText = $"{tierLine}\nAccept a quest to put something in the fire.";
            else
            {
                var def = view.Program.Definition;
                var done = await _programs.CompletedDaysInBlockAsync(view.Program.BlockId);
                ArmouryText = $"{tierLine}\nIn the fire: the {HeroQuest.Name(def)} · {done} of {Forging.Needed(def.TrainingDaysPerWeek)} training days needed.";
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
