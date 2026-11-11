using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.ViewModels
{
    /// <summary>
    /// The in-gym logging screen (PBI 8.1). Built to be used mid-set at arm's length: big numbers,
    /// big targets, everything pre-filled so logging a set is one tap.
    /// </summary>
    public sealed class ActiveSessionViewModel : BaseViewModel
    {
        private readonly ITrainingService _training;
        private IDispatcherTimer? _clock;
        private DateTime _startedUtc;

        public int SessionId { get; private set; }
        public ObservableCollection<ExerciseCardViewModel> Exercises { get; } = new();

        private string _sessionTitle = "";
        public string SessionTitle { get => _sessionTitle; private set => SetProperty(ref _sessionTitle, value); }

        private string _locationText = "";
        public string LocationText { get => _locationText; private set => SetProperty(ref _locationText, value); }

        private string _elapsedText = "0:00";
        public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

        private string _progressText = "";
        public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

        public ICommand FinishCommand { get; }
        public ICommand DiscardCommand { get; }
        public ICommand AddExerciseCommand { get; }

        /// <summary>Raised when the session ends (finished or discarded) so the page can close.</summary>
        public event EventHandler<SessionSnapshot?>? Ended;

        internal ITrainingService Training => _training;
        internal Equipment AvailableEquipment { get; private set; }
        internal IReadOnlyList<ExerciseInfo> Library { get; private set; } = Array.Empty<ExerciseInfo>();

        public ActiveSessionViewModel(ITrainingService training)
        {
            _training = training;
            FinishCommand = new Command(async () => await FinishAsync());
            DiscardCommand = new Command(async () => await DiscardAsync());
            AddExerciseCommand = new Command(async () => await AddExerciseAsync());
        }

        public async Task LoadAsync(int sessionId)
        {
            SessionId = sessionId;
            var snap = await _training.GetSessionAsync(sessionId);
            Library = await _training.GetLibraryAsync();
            AvailableEquipment = await _training.GetEquipmentAsync(snap.Location);

            SessionTitle = snap.Session.Title ?? "Session";
            LocationText = snap.Location == TrainingLocation.Gym ? "GYM" : "HOME";
            _startedUtc = new DateTime(snap.Session.StartedAtUtc, DateTimeKind.Utc);

            Exercises.Clear();
            foreach (var ex in snap.Exercises)
            {
                var card = new ExerciseCardViewModel(this, ex);
                await card.LoadHistoryAsync();
                Exercises.Add(card);
            }
            RefreshProgress();
            Tick();
        }

        /// <summary>Reloads one exercise card after a structural change (swap, sets).</summary>
        internal async Task ReloadAsync()
        {
            var snap = await _training.GetSessionAsync(SessionId);
            // Rebuild only cards whose identity changed; keep the in-progress inputs of the rest.
            var byId = Exercises.ToDictionary(c => c.Id);
            Exercises.Clear();
            foreach (var ex in snap.Exercises)
            {
                if (byId.TryGetValue(ex.Row.Id, out var card) && card.Name == ex.Row.ExerciseName)
                {
                    card.Update(ex);
                    Exercises.Add(card);
                }
                else
                {
                    var fresh = new ExerciseCardViewModel(this, ex);
                    await fresh.LoadHistoryAsync();
                    Exercises.Add(fresh);
                }
            }
            RefreshProgress();
        }

        internal void RefreshProgress()
        {
            var active = Exercises.Where(e => !e.IsSkipped).ToList();
            var done = active.Count(e => e.IsComplete);
            ProgressText = $"{done}/{active.Count} exercises";
            var current = active.FirstOrDefault(e => !e.IsComplete);
            foreach (var e in Exercises) e.IsCurrent = ReferenceEquals(e, current);
        }

        public void StartClock()
        {
            if (_clock is not null) return;
            _clock = Application.Current!.Dispatcher.CreateTimer();
            _clock.Interval = TimeSpan.FromSeconds(1);
            _clock.Tick += (_, _) => Tick();
            _clock.Start();
        }

        public void StopClock()
        {
            _clock?.Stop();
            _clock = null;
        }

        private void Tick()
        {
            var e = DateTime.UtcNow - _startedUtc;
            ElapsedText = e.TotalHours >= 1 ? e.ToString(@"h\:mm\:ss") : e.ToString(@"m\:ss");
        }

        private async Task AddExerciseAsync()
        {
            var doable = Library
                .Where(e => e.Category == ExerciseCategory.Strength && ExerciseSwap.IsAvailable(e, AvailableEquipment))
                .Select(e => e.Name).ToArray();
            var pick = await Shell.Current.DisplayActionSheet("Add exercise", "Cancel", null, doable);
            if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
            await _training.AddExerciseAsync(SessionId, pick);
            await ReloadAsync();
        }

        private async Task FinishAsync()
        {
            var logged = Exercises.Sum(e => e.WorkingSetCount);
            var message = logged == 0
                ? "No working sets logged yet. Finish anyway? An empty session won't complete the program day."
                : $"Finish and save {logged} working set{(logged == 1 ? "" : "s")}?";
            if (!await Shell.Current.DisplayAlert("Finish session", message, "Finish", "Keep going")) return;

            StopClock();
            var snap = await _training.FinishAsync(SessionId);
            Ended?.Invoke(this, snap);
        }

        private async Task DiscardAsync()
        {
            if (!await Shell.Current.DisplayAlert("Discard session",
                    "Throw this session away? Nothing from it will count.", "Discard", "Keep it")) return;
            StopClock();
            await _training.DiscardAsync(SessionId);
            Ended?.Invoke(this, null);
        }
    }

    /// <summary>A logged set as shown in the list.</summary>
    public sealed class SetLineViewModel
    {
        public required int Id { get; init; }
        public required string NumberText { get; init; }
        public required string LoadText { get; init; }
        public required string WorkText { get; init; }
        public bool IsWarmup { get; init; }
    }

    public sealed class ExerciseCardViewModel : BaseViewModel
    {
        private readonly ActiveSessionViewModel _owner;
        private SessionExerciseSnapshot _snap;
        private LastPerformance? _last;

        public ObservableCollection<SetLineViewModel> Sets { get; } = new();

        public int Id => _snap.Row.Id;
        public string Name => _snap.Row.ExerciseName;
        public bool IsTimed => _snap.IsTimed;
        public bool IsRepBased => !IsTimed;
        public string? SwappedFromText => _snap.Row.SwappedFrom is { } from ? $"swapped for {from}" : null;
        public bool HasSwap => _snap.Row.SwappedFrom is not null;

        private bool _isSkipped;
        public bool IsSkipped { get => _isSkipped; private set { if (SetProperty(ref _isSkipped, value)) OnPropertyChanged(nameof(ShowInputs)); } }

        private bool _isCurrent;
        public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }

        public string PrescriptionText
        {
            get
            {
                var r = _snap.Row;
                var per = r.TargetSeconds is int s ? $"{s}s"
                    : r.RepsMin is int lo && r.RepsMax is int hi ? (lo == hi ? $"{lo}" : $"{lo}–{hi}")
                    : "reps";
                var rest = r.RestSeconds is int rs ? $" · rest {rs / 60}:{rs % 60:00}" : "";
                return $"{r.TargetSets ?? 3} × {per}{rest}";
            }
        }

        private string? _lastText;
        public string? LastText { get => _lastText; private set => SetProperty(ref _lastText, value); }
        public bool HasLast => LastText is not null;

        public int WorkingSetCount => _snap.Sets.Count(s => !s.IsWarmup);
        public bool IsComplete => WorkingSetCount >= (_snap.Row.TargetSets ?? 3);
        public string SetCountText => $"{WorkingSetCount}/{_snap.Row.TargetSets ?? 3}";
        public bool ShowInputs => !IsSkipped;

        // --- inputs (display unit) ---

        private WeightUnit Unit => UserSettings.WeightUnit;
        public string UnitLabel => WeightMath.UnitLabel(Unit);

        /// <summary>Bodyweight moves log <i>added</i> load (vest, backpack) — 0 is the normal case.</summary>
        public string LoadCaption => _snap.Info?.Modality == Modality.Bodyweight
            ? $"ADDED ({UnitLabel})"
            : $"LOAD ({UnitLabel})";
        private double Step => LoadIncrements.StepFor(_snap.Info?.Modality ?? Modality.Dumbbell, Unit);

        private double _load;
        public double Load { get => _load; set { if (SetProperty(ref _load, Math.Max(0, value))) OnPropertyChanged(nameof(LoadText)); } }
        public string LoadText => Load.ToString("0.#", CultureInfo.CurrentCulture);

        private int _reps;
        public int Reps { get => _reps; set => SetProperty(ref _reps, Math.Max(0, value)); }

        private int _seconds;
        public int Seconds { get => _seconds; set => SetProperty(ref _seconds, Math.Max(0, value)); }

        private bool _isWarmup;
        public bool IsWarmup { get => _isWarmup; set { if (SetProperty(ref _isWarmup, value)) OnPropertyChanged(nameof(LogButtonText)); } }
        public string LogButtonText => IsWarmup ? "LOG WARM-UP" : "LOG SET";

        public ICommand LoadUpCommand { get; }
        public ICommand LoadDownCommand { get; }
        public ICommand RepsUpCommand { get; }
        public ICommand RepsDownCommand { get; }
        public ICommand SecondsUpCommand { get; }
        public ICommand SecondsDownCommand { get; }
        public ICommand EditLoadCommand { get; }
        public ICommand EditRepsCommand { get; }
        public ICommand EditSecondsCommand { get; }
        public ICommand ToggleWarmupCommand { get; }
        public ICommand LogCommand { get; }
        public ICommand MenuCommand { get; }
        public ICommand DeleteSetCommand { get; }

        public ExerciseCardViewModel(ActiveSessionViewModel owner, SessionExerciseSnapshot snap)
        {
            _owner = owner;
            _snap = snap;
            _isSkipped = snap.Row.Skipped;

            LoadUpCommand = new Command(() => Load = LoadIncrements.Snap(Load + Step, Step));
            LoadDownCommand = new Command(() => Load = LoadIncrements.Snap(Load - Step, Step));
            RepsUpCommand = new Command(() => Reps++);
            RepsDownCommand = new Command(() => Reps--);
            SecondsUpCommand = new Command(() => Seconds += 5);
            SecondsDownCommand = new Command(() => Seconds -= 5);
            ToggleWarmupCommand = new Command(() => IsWarmup = !IsWarmup);
            // Tap a number to type it — stepping a barbell from 0 to 225 one plate at a time is silly.
            EditLoadCommand = new Command(async () =>
            {
                if (await PromptNumberAsync($"Load ({UnitLabel})", LoadText) is double v) Load = v;
            });
            EditRepsCommand = new Command(async () =>
            {
                if (await PromptNumberAsync("Reps", Reps.ToString(CultureInfo.CurrentCulture)) is double v) Reps = (int)Math.Round(v);
            });
            EditSecondsCommand = new Command(async () =>
            {
                if (await PromptNumberAsync("Seconds", Seconds.ToString(CultureInfo.CurrentCulture)) is double v) Seconds = (int)Math.Round(v);
            });
            LogCommand = new Command(async () => await LogAsync());
            MenuCommand = new Command(async () => await MenuAsync());
            DeleteSetCommand = new Command<int>(async id => await DeleteSetAsync(id));

            RebuildSets();
        }

        internal void Update(SessionExerciseSnapshot snap)
        {
            _snap = snap;
            IsSkipped = snap.Row.Skipped;
            RebuildSets();
            OnPropertyChanged(string.Empty); // refresh every derived binding
        }

        internal async Task LoadHistoryAsync()
        {
            _last = await _owner.Training.GetLastPerformanceAsync(Name, _owner.SessionId);
            LastText = _last is null ? null : $"Last ({_last.Date:MMM d}): " + string.Join(", ", _last.WorkingSets.Select(Describe));
            OnPropertyChanged(nameof(HasLast));
            Prefill();
        }

        /// <summary>
        /// Pre-fill from the previous set in this session, else the same set last time, else the
        /// prescription. PBI 8.5 replaces the load with a progression suggestion.
        /// </summary>
        private void Prefill()
        {
            var lastHere = _snap.Sets.LastOrDefault();
            var index = WorkingSetCount;
            var lastTime = _last?.WorkingSets.ElementAtOrDefault(index) ?? _last?.WorkingSets.LastOrDefault();

            var loadLb = lastHere?.LoadLb ?? lastTime?.LoadLb ?? 0;
            Load = LoadIncrements.Snap(WeightMath.ToDisplay(loadLb, Unit), Step);
            Reps = lastHere?.Reps ?? lastTime?.Reps ?? _snap.Row.RepsMin ?? 8;
            Seconds = lastHere?.Seconds ?? lastTime?.Seconds ?? _snap.Row.TargetSeconds ?? 30;
        }

        private string Describe(SessionSetRow s)
        {
            var load = s.LoadLb > 0 ? WeightMath.ToDisplay(s.LoadLb, Unit).ToString("0.#", CultureInfo.CurrentCulture) : "BW";
            var work = s.Seconds is int sec ? $"{sec}s" : $"{s.Reps}";
            return s.LoadLb > 0 ? $"{load}×{work}" : $"{work}";
        }

        private void RebuildSets()
        {
            Sets.Clear();
            var n = 0;
            foreach (var s in _snap.Sets)
            {
                var label = s.IsWarmup ? "W" : (++n).ToString(CultureInfo.CurrentCulture);
                Sets.Add(new SetLineViewModel
                {
                    Id = s.Id,
                    NumberText = label,
                    LoadText = s.LoadLb > 0
                        ? $"{WeightMath.ToDisplay(s.LoadLb, Unit).ToString("0.#", CultureInfo.CurrentCulture)} {UnitLabel}"
                        : "bodyweight",
                    WorkText = s.Seconds is int sec ? $"{sec}s" : $"× {s.Reps}",
                    IsWarmup = s.IsWarmup
                });
            }
        }

        private static async Task<double?> PromptNumberAsync(string title, string initial)
        {
            var text = await Shell.Current.DisplayPromptAsync(title, null, "OK", "Cancel",
                initialValue: initial, maxLength: 7, keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var v) && v >= 0 ? v : null;
        }

        private async Task LogAsync()
        {
            var loadLb = WeightMath.ToStoredPounds(Load, Unit);
            await _owner.Training.LogSetAsync(Id, loadLb, IsTimed ? null : Reps, IsTimed ? Seconds : null, IsWarmup);
            IsWarmup = false; // warm-up is a per-set choice, never sticky
            await _owner.ReloadAsync();
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* not all devices */ }
        }

        private async Task DeleteSetAsync(int setId)
        {
            if (!await Shell.Current.DisplayAlert("Delete set", "Remove this set?", "Delete", "Cancel")) return;
            await _owner.Training.DeleteSetAsync(setId);
            await _owner.ReloadAsync();
        }

        private async Task MenuAsync()
        {
            const string swap = "Swap exercise", skip = "Skip", unskip = "Un-skip", up = "Move up", down = "Move down";
            var choice = await Shell.Current.DisplayActionSheet(Name, "Cancel", null, swap, IsSkipped ? unskip : skip, up, down);
            switch (choice)
            {
                case swap: await SwapAsync(); break;
                case skip: await _owner.Training.SetSkippedAsync(Id, true); await _owner.ReloadAsync(); break;
                case unskip: await _owner.Training.SetSkippedAsync(Id, false); await _owner.ReloadAsync(); break;
                case up: await _owner.Training.MoveExerciseAsync(Id, -1); await _owner.ReloadAsync(); break;
                case down: await _owner.Training.MoveExerciseAsync(Id, +1); await _owner.ReloadAsync(); break;
            }
        }

        private async Task SwapAsync()
        {
            // Substitutes for the program's exercise (or this one, if it wasn't swapped), doable here.
            var originalName = _snap.Row.SwappedFrom ?? Name;
            var original = _owner.Library.FirstOrDefault(e => e.Name == originalName) ?? _snap.Info;
            var options = new List<string>();
            if (_snap.Row.SwappedFrom is not null) options.Add($"↩ {originalName}");
            if (original is not null)
                options.AddRange(ExerciseSwap.Candidates(original, _owner.Library, _owner.AvailableEquipment)
                    .Where(e => e.Name != Name).Select(e => e.Name));
            const string any = "Any exercise…";
            options.Add(any);

            var pick = await Shell.Current.DisplayActionSheet($"Swap {Name}", "Cancel", null, options.ToArray());
            if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
            if (pick == any)
            {
                var all = _owner.Library
                    .Where(e => e.Category == ExerciseCategory.Strength && ExerciseSwap.IsAvailable(e, _owner.AvailableEquipment) && e.Name != Name)
                    .Select(e => e.Name).ToArray();
                pick = await Shell.Current.DisplayActionSheet("Swap to", "Cancel", null, all);
                if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
            }
            if (pick.StartsWith("↩ ", StringComparison.Ordinal)) pick = pick[2..];

            await _owner.Training.SwapExerciseAsync(Id, pick);
            await _owner.ReloadAsync();
        }
    }
}
