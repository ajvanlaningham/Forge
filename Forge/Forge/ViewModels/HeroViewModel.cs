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
    public sealed record RackItem(string Name, string? Sprite, double Opacity, int Column, int Row);

    /// <summary>One stat's test history on the Hero tab: latest score, change, trend, raw results.</summary>
    public sealed class StatHistoryItem
    {
        public required string Label { get; init; }
        public required string ScoreText { get; init; }
        public string ChangeText { get; init; } = "";
        public required string DetailText { get; init; }
        public required IDrawable Trend { get; init; }
        public bool HasTrend { get; init; }
        /// <summary>Tap the row: how this compares with published norms, where any apply (Decision 23).</summary>
        public required ICommand CompareCommand { get; init; }
    }

    public sealed class HeroViewModel : BaseViewModel
    {
        private readonly IStatsService _stats;
        private readonly IProgramService _programs;
        private readonly IInventoryService _inventory;
        private readonly ProgressService _progress;
        private readonly IForgeService _forge;
        private readonly IStatTestService _tests;

        public StatCardViewModel StatsCard { get; } = new() { ShowStats = false };
        public ObservableCollection<GearItem> Pack { get; } = new();

        private string _armouryText = "";
        public string ArmouryText { get => _armouryText; private set => SetProperty(ref _armouryText, value); }

        private string _streakText = "";
        public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

        private bool _packEmpty;
        public bool PackEmpty { get => _packEmpty; private set => SetProperty(ref _packEmpty, value); }

        public ObservableCollection<RackItem> Rack { get; } = new();
        public ObservableCollection<StatHistoryItem> TestHistory { get; } = new();

        private string _spriteFile = UserSettings.HeroSpriteFile;
        public string SpriteFile { get => _spriteFile; private set => SetProperty(ref _spriteFile, value); }
        private string _heroTitle = UserSettings.HeroTitle;
        public string HeroTitle { get => _heroTitle; private set => SetProperty(ref _heroTitle, value); }
        public ICommand ChangeLookCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(HeroPickerPage)));

        public ICommand EditPackCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(MyGearPage)));

        /// <summary>A test outside its slotted day: still scored (or the baseline, if there isn't one yet).</summary>
        public ICommand TakeTestCommand { get; } = new Command(async () =>
        {
            var pick = await Shell.Current.DisplayActionSheetAsync("Take a stat test", "Cancel", null, "Strength", "Mobility", "Conditioning");
            var kind = pick switch { "Strength" => "Strength", "Mobility" => "Dexterity", "Conditioning" => "Constitution", _ => null };
            if (kind is not null) await Shell.Current.GoToAsync($"{nameof(StatTestPage)}?kind={kind}&calibration=false");
        });

        public HeroViewModel(IStatsService stats, IProgramService programs, IInventoryService inventory, ProgressService progress,
            IForgeService forge, IStatTestService tests)
        {
            _tests = tests;
            _forge = forge;
            _stats = stats;
            _programs = programs;
            _inventory = inventory;
            _progress = progress;
            Title = "Hero";
        }

        private async Task LoadTestHistoryAsync()
        {
            var accent = (Color)Application.Current!.Resources["Accent"];
            var line = (Color)Application.Current.Resources["Muted"];
            TestHistory.Clear();
            foreach (var (kind, label) in new[] { (StatKind.Strength, "STR · Strength"), (StatKind.Dexterity, "DEX · Mobility"), (StatKind.Constitution, "CON · Conditioning") })
            {
                var history = await _tests.HistoryAsync(kind);
                var scored = history.Where(t => !t.Calibration).ToList();
                var last = history.LastOrDefault();
                string detail;
                if (last is null)
                    detail = "No test yet. Tests come up on Today in week 1 (a practice run) and in Honing week.";
                else
                {
                    var raw = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(last.DetailJson) ?? new();
                    var when = DateOnly.ParseExact(last.DateKey, "yyyy-MM-dd").ToString("MMM d", System.Globalization.CultureInfo.CurrentCulture);
                    detail = $"{(last.Calibration ? "Practice run" : "Last test")} · {when}\n" + string.Join("\n", raw.Select(kv => $"{kv.Key}: {kv.Value}"));
                }
                var latest = scored.LastOrDefault();
                var change = scored.Count >= 2 ? scored[^1].Score - scored[^2].Score : (int?)null;
                TestHistory.Add(new StatHistoryItem
                {
                    Label = label,
                    ScoreText = latest is null ? "—" : latest.Score.ToString(System.Globalization.CultureInfo.CurrentCulture),
                    ChangeText = change is int c ? $"{(c >= 0 ? "+" : "")}{c} since last" : latest is not null ? "baseline" : "",
                    DetailText = detail,
                    Trend = new Forge.ViewModels.Controls.Sparkline(scored.Select(t => (double)t.Score).ToList(), line, accent),
                    HasTrend = scored.Count >= 2,
                    CompareCommand = new Command(async () => await Shell.Current.DisplayAlertAsync("How this compares", CompareText(kind, last), "OK"))
                });
            }
        }

        /// <summary>
        /// Coarse published-norm bands, only where a credible table matches how we run the test.
        /// Today that's push-ups (CSEP-PATH); the others say plainly why there's no comparison.
        /// </summary>
        private static string CompareText(StatKind kind, StatTestRow? last)
        {
            const string self = "Your score only ever compares you with yourself.";
            if (kind != StatKind.Strength)
                return $"There's no published table that matches this test as Forge runs it, so there's no comparison here. {self}";
            var group = UserSettings.NormsGroup;
            if (group == "none" || UserSettings.BirthYear is not int year)
                return $"Comparisons are off. To see push-ups against published norms, choose a set of norms and add your birth year in Settings → Compare my test results. {self}";
            var push = last is null ? null : (System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(last.MeasuresJson) ?? new())
                .FirstOrDefault(kv => kv.Key.StartsWith("pushups:", StringComparison.Ordinal)) is { Key: not null } kv ? kv : (KeyValuePair<string, double>?)null;
            if (push is null) return $"No push-up result yet. {self}";
            var version = push.Value.Key["pushups:".Length..];
            var reps = (int)push.Value.Value;
            var age = DateTime.Today.Year - year;
            var groupName = group == "women" ? "women" : "men";
            if (Norms.PushUpBand(group, age, version, reps) is { } band)
                return $"Push-ups: {reps} · {band} for {groupName} {Norms.AgeBand(age)}.\n\nSource: {Norms.PushUpSource}. A coarse band, not a health verdict. {self}";
            var needs = group == "women" ? "knee push-ups" : "full push-ups from the floor";
            return age is < 20 or > 69
                ? $"The published table covers ages 20–69. {self}"
                : $"The {groupName}'s table is for {needs}; your test is '{version}', so it doesn't apply. {self}";
        }

        public async Task RefreshAsync()
        {
            SpriteFile = UserSettings.HeroSpriteFile;
            HeroTitle = UserSettings.HeroTitle;
            StatsCard.Title = "Level";
            await _stats.InitAsync();
            var core = await _stats.GetCoreStatsAsyncFromDb();
            StatsCard.ApplyXp((await _stats.GetUserStatsAsync()).Xp);
            StatsCard.Strength = core.FirstOrDefault(s => s.Kind == StatKind.Strength)?.Score ?? 1;
            StatsCard.Dexterity = core.FirstOrDefault(s => s.Kind == StatKind.Dexterity)?.Score ?? 1;
            StatsCard.Constitution = core.FirstOrDefault(s => s.Kind == StatKind.Constitution)?.Score ?? 1;

            await LoadTestHistoryAsync();

            var (current, longest) = await _progress.GetStreakAsync();
            StreakText = $"Moved {current} day{(current == 1 ? "" : "s")} in a row · best {Math.Max(current, longest)}";

            var view = await _programs.GetViewAsync(DateOnly.FromDateTime(DateTime.Today));
            // The rack (Decision 25): five slots, each in the metal it has reached. A bare slot shows
            // faintly in Copper, the metal its first forging will make.
            var forged = await _forge.GetForgedAsync();
            Rack.Clear();
            for (var i = 0; i < Armoury.Slots.Length; i++)
            {
                var slot = Armoury.Slots[i];
                var metal = Armoury.MetalOf(forged, slot);
                Rack.Add(new RackItem(metal is null ? $"{Armoury.Label(slot)}: bare" : $"{metal} {HeroQuest.ItemName(slot)}",
                    HeroQuest.Sprite(slot, metal ?? "Copper"), metal is null ? 0.22 : 1.0, i % 3, i / 3));
            }
            var spent = forged.Count;
            var unspent = (await _forge.UnspentAsync()).Count;
            var lines = new List<string> { $"{spent} of {Armoury.MaxForgings} forgings. Every quest you finish earns one, for any slot." };
            if (unspent > 0) lines.Add($"{unspent} forging{(unspent == 1 ? "" : "s")} ready to spend.");
            if (view is not null)
            {
                var def = view.Program.Definition;
                var done = await _programs.CompletedDaysInBlockAsync(view.Program.BlockId);
                lines.Add($"{def.Name}: {done} of {Forging.Needed(def.TrainingDaysPerWeek)} training days for the next forging.");
            }
            else lines.Add("Accept a quest to earn your next forging.");
            ArmouryText = string.Join("\n", lines);

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
