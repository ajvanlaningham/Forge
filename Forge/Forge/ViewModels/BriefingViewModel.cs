using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.Views;
using Forge.Views.SubPages;

namespace Forge.ViewModels
{
    /// <summary>One exercise in the briefing, with its weight settled before BEGIN.</summary>
    public sealed class BriefItem : BaseViewModel
    {
        private readonly BriefingViewModel _owner;
        internal PlannedExercise Plan { get; }
        internal LoadSet Loads { get; }

        public string Name => Plan.Name;
        public string? GroupLabel { get; init; }
        public bool HasGroup => GroupLabel is not null;
        public string? SwapText => Plan.SwappedFrom is { } from ? $"swapped for {from}" : null;
        public bool HasSwap => SwapText is not null;
        public bool IsTimed => Plan.Slot.IsTimed;
        public bool ShowLoad => GuidedSupport.IsLoaded(Plan.Info);
        public string? Note { get; init; }
        public bool HasNote => Note is not null;

        public string PrescriptionText
        {
            get
            {
                var s = Plan.Slot;
                var per = s.IsTimed ? $"{Plan.PlannedSeconds ?? s.Seconds}s"
                    : $"{Plan.PlannedReps ?? s.RepsMin}" + (s.RepsMin != s.RepsMax ? $"  ({s.RepsMin}–{s.RepsMax})" : "");
                return $"{s.Sets} × {per}";
            }
        }

        public string LoadText => _owner.Gear.LoadLabel(Plan.PlannedLoadLb, UserSettings.WeightUnit);

        internal IReadOnlyList<WarmupSet> Warmups => GuidedSupport.Warmups(Plan.IsMain, Plan.Info, IsTimed, Plan.PlannedLoadLb, Loads);
        public bool Warmup => Warmups.Count > 0;
        /// <summary>"Warm-up: 20 lb × 8 (50%) → 30 lb × 4 (75%)".</summary>
        public string? WarmupText => Warmup
            ? "Warm-up: " + string.Join(" → ", Warmups.Select(w =>
                $"{_owner.Gear.LoadLabel(w.LoadLb, UserSettings.WeightUnit)} × {w.Reps} ({w.Percent}%)"))
            : null;

        public ICommand LoadUpCommand { get; }
        public ICommand LoadDownCommand { get; }

        public BriefItem(BriefingViewModel owner, PlannedExercise plan, LoadSet loads)
        {
            _owner = owner;
            Plan = plan;
            Loads = loads;
            LoadUpCommand = new Command(() => { if (Loads.Next(Plan.PlannedLoadLb) is double up) SetLoad(up); });
            LoadDownCommand = new Command(() => SetLoad(Loads.Prev(Plan.PlannedLoadLb) ?? 0));
        }

        private void SetLoad(double lb)
        {
            Plan.PlannedLoadLb = lb;
            OnPropertyChanged(nameof(LoadText));
            OnPropertyChanged(nameof(Warmup));
            OnPropertyChanged(nameof(WarmupText));
            _owner.Recompute();
        }
    }

    /// <summary>
    /// The briefing before a guided session: where, what, which weights, what to gather. Nothing is
    /// timed or logged until BEGIN.
    /// </summary>
    public sealed class BriefingViewModel : BaseViewModel
    {
        private readonly ITrainingService _training;
        private SessionPlan? _plan;
        internal GearLoads Gear { get; private set; } = new();

        public ObservableCollection<BriefItem> Items { get; } = new();

        private TrainingLocation _location = UserSettings.LastLocation;
        public bool AtHome => _location == TrainingLocation.Home;
        public bool AtGym => _location == TrainingLocation.Gym;

        private string _gatherText = "";
        public string GatherText { get => _gatherText; private set => SetProperty(ref _gatherText, value); }

        private string _estimateText = "";
        public string EstimateText { get => _estimateText; private set => SetProperty(ref _estimateText, value); }

        private string _subtitle = "";
        public string Subtitle { get => _subtitle; private set => SetProperty(ref _subtitle, value); }

        public ICommand HomeCommand { get; }
        public ICommand GymCommand { get; }
        public ICommand BeginCommand { get; }

        public BriefingViewModel(ITrainingService training)
        {
            _training = training;
            HomeCommand = new Command(async () => await SetLocationAsync(TrainingLocation.Home));
            GymCommand = new Command(async () => await SetLocationAsync(TrainingLocation.Gym));
            BeginCommand = new Command(async () => await BeginAsync());
        }

        private async Task SetLocationAsync(TrainingLocation where)
        {
            if (where == _location) return;
            _location = where;
            OnPropertyChanged(nameof(AtHome));
            OnPropertyChanged(nameof(AtGym));
            await LoadAsync();
        }

        public async Task LoadAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            _plan = await _training.PlanFromProgramAsync(today, _location);
            Title = _plan.Title;
            Subtitle = $"{HeroQuest.Name(_plan.Program.Definition)} · {_plan.Program.Definition.VersionName}";
            Gear = UserSettings.GearAt(_location);
            var unit = UserSettings.WeightUnit;
            var groups = _plan.Exercises.Select(e => e.Slot.Superset).ToList();

            Items.Clear();
            for (var i = 0; i < _plan.Exercises.Count; i++)
            {
                var p = _plan.Exercises[i];
                var loads = Gear.For(p.Info?.Modality ?? Modality.Bodyweight, unit, p.Info?.NeedsPair ?? false);
                var history = await _training.GetRecentWorkingSetsAsync(p.Name);
                var rx = new Prescription(p.Slot.Sets, p.Slot.RepsMin, p.Slot.RepsMax, p.Slot.Seconds);
                var suggestion = Progression.Suggest(rx, history, loads, unit);
                var last = history.FirstOrDefault(h => h.Count > 0);

                string? note;
                if (suggestion is not null) note = suggestion.Reason;
                else if (GuidedSupport.IsLoaded(p.Info)) note = "First time: pick a weight you could lift a few more times than asked.";
                else note = null;

                var load = suggestion?.LoadLb ?? last?.Max(s => s.LoadLb)
                           ?? (GuidedSupport.IsLoaded(p.Info) ? loads.Next(0) ?? 0 : 0);
                p.PlannedLoadLb = load > 0 ? loads.Snap(load) : 0;
                p.PlannedReps = p.Slot.IsTimed ? null : suggestion?.Reps ?? p.Slot.RepsMin;
                p.PlannedSeconds = p.Slot.IsTimed ? suggestion?.Seconds ?? p.Slot.Seconds : null;

                Items.Add(new BriefItem(this, p, loads) { GroupLabel = GuidedSupport.GroupLabel(groups, i), Note = note });
            }
            Recompute();
        }

        /// <summary>Gather list and time estimate; re-run when a weight changes.</summary>
        internal void Recompute()
        {
            if (_plan is null) return;
            var unit = UserSettings.WeightUnit;

            // Implements and weights, each needed count once (a pair for two-handed lifts).
            var weights = new Dictionary<(Modality, double), int>();
            var other = Equipment.None;
            foreach (var item in Items)
            {
                var p = item.Plan;
                var mod = p.Info?.Modality ?? Modality.Bodyweight;
                var count = p.Info?.NeedsPair == true ? 2 : 1;
                if (mod is Modality.Dumbbell or Modality.Kettlebell && p.PlannedLoadLb > 0)
                {
                    Need(mod, p.PlannedLoadLb, count);
                    foreach (var w in item.Warmups) Need(mod, w.LoadLb, count);
                }
                other |= (p.Info?.Equipment ?? Equipment.None) & ~(Equipment.Dumbbell | Equipment.Kettlebell);
            }
            void Need(Modality m, double lb, int n) => weights[(m, lb)] = Math.Max(weights.GetValueOrDefault((m, lb)), n);

            var lines = new List<string>();
            foreach (var (mod, label) in new[] { (Modality.Dumbbell, "Dumbbells"), (Modality.Kettlebell, "Kettlebell") })
            {
                var these = weights.Where(w => w.Key.Item1 == mod).OrderBy(w => w.Key.Item2)
                    .Select(w => Gear.LoadLabel(w.Key.Item2, unit) + (w.Value > 1 ? " ×2" : "")).ToList();
                if (these.Count > 0) lines.Add($"{label}: {string.Join(" · ", these)}");
            }
            var extras = Enum.GetValues<Equipment>().Where(e => e != Equipment.None && other.HasFlag(e))
                .Select(e => new GearItem { Equipment = e, Name = e.ToString() }.DisplayName).ToList();
            if (extras.Count > 0) lines.Add(string.Join(" · ", extras));
            GatherText = lines.Count > 0 ? string.Join("\n", lines) : "Nothing: bodyweight only.";

            var steps = Steps();
            var minutes = GuidedSupport.EstimateMinutes(steps, i => (_plan.Exercises[i].PlannedReps, _plan.Exercises[i].PlannedSeconds));
            EstimateText = $"About {minutes} min · {steps.Count} sets";
        }

        private IReadOnlyList<GuidedStep> Steps()
            => GuidedPlan.Build(Items.Select((it, i) => new GuidedExercise(i, it.Plan.Slot.Superset, it.Plan.Slot.Sets,
                it.Plan.Slot.RestSeconds, it.Warmups.Count)).ToList(), UserSettings.DefaultRestSeconds);

        private async Task BeginAsync()
        {
            if (_plan is null || IsBusy) return;
            IsBusy = true;
            try
            {
                if (_location == TrainingLocation.Home && await _training.GetEquipmentAsync(_location) == Equipment.None)
                {
                    var setUp = await Shell.Current.DisplayAlert("No home gear set up",
                        "Forge doesn't know what equipment you have at home, so every lift swaps to a bodyweight move. Set up your gear first?",
                        "Set up gear", "Start anyway");
                    if (setUp) { await Shell.Current.GoToAsync(nameof(MyGearPage)); return; }
                }
                var snap = await _training.StartFromPlanAsync(_plan);
                // Replace the briefing with the session: back from the session goes to Today.
                await Shell.Current.GoToAsync($"../{nameof(GuidedSessionPage)}?sessionId={snap.Session.Id}");
            }
            finally { IsBusy = false; }
        }
    }
}
