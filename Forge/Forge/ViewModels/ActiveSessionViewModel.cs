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
        private readonly IRestAlarm _alarm;
        private IDispatcherTimer? _clock;
        private DateTime _startedUtc;

        // Rest timer state mirrors the session row: an end time, or seconds left while paused.
        private DateTime? _restEndsUtc;
        private int? _restPausedSeconds;
        private int _restTotalSeconds = 1;
        private DateTime? _restOverShownUtc;

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

        private bool _isResting;
        public bool IsResting { get => _isResting; private set => SetProperty(ref _isResting, value); }

        private bool _isRestOver;
        public bool IsRestOver { get => _isRestOver; private set => SetProperty(ref _isRestOver, value); }

        private string _restText = "";
        public string RestText { get => _restText; private set => SetProperty(ref _restText, value); }

        private string _restCaption = "REST";
        public string RestCaption { get => _restCaption; private set => SetProperty(ref _restCaption, value); }

        private double _restProgress;
        public double RestProgress { get => _restProgress; private set => SetProperty(ref _restProgress, value); }

        private string _pauseText = "PAUSE";
        public string PauseText { get => _pauseText; private set => SetProperty(ref _pauseText, value); }

        public ICommand RestPauseCommand { get; }
        public ICommand RestPlusCommand { get; }
        public ICommand RestMinusCommand { get; }
        public ICommand RestSkipCommand { get; }

        public ICommand FinishCommand { get; }
        public ICommand DiscardCommand { get; }
        public ICommand AddExerciseCommand { get; }

        /// <summary>Raised when the session ends (finished or discarded) so the page can close.</summary>
        public event EventHandler<SessionSnapshot?>? Ended;

        internal ITrainingService Training => _training;
        internal Equipment AvailableEquipment { get; private set; }
        /// <summary>Weights available where this session is happening (Decision 16).</summary>
        internal GearLoads Gear { get; private set; } = new();
        internal IReadOnlyList<ExerciseInfo> Library { get; private set; } = Array.Empty<ExerciseInfo>();

        public ActiveSessionViewModel(ITrainingService training, IRestAlarm alarm)
        {
            _training = training;
            _alarm = alarm;
            RestPauseCommand = new Command(async () => ApplyRest(await _training.ToggleRestPauseAsync(SessionId)));
            RestPlusCommand = new Command(async () => ApplyRest(await _training.AdjustRestAsync(SessionId, +15)));
            RestMinusCommand = new Command(async () => ApplyRest(await _training.AdjustRestAsync(SessionId, -15)));
            RestSkipCommand = new Command(async () => ApplyRest(await _training.StopRestAsync(SessionId)));
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
            Gear = UserSettings.GearAt(snap.Location);

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
            ApplyRest(snap.Session);
            Tick();

            // Ask once, up front, rather than mid-set. Denial just means in-app alerts only.
            try { await _alarm.EnsurePermissionAsync(); } catch { /* permission UI unavailable */ }
        }

        /// <summary>Starts the rest countdown after a set (PBI 8.3).</summary>
        internal async Task StartRestAsync(int? slotRestSeconds, bool afterWarmup, string exerciseName)
        {
            var seconds = slotRestSeconds ?? UserSettings.DefaultRestSeconds;
            if (afterWarmup) seconds = Math.Min(seconds, 60); // warm-ups don't need full rest
            _restTotalSeconds = Math.Max(1, seconds);
            _nextUpHint = exerciseName;
            ApplyRest(await _training.StartRestAsync(SessionId, seconds));
        }

        private string _nextUpHint = "";

        /// <summary>Syncs local state and the OS alarm from the persisted session row.</summary>
        private void ApplyRest(TrainingSessionRow row)
        {
            _restEndsUtc = row.RestEndsAtUtc is long t ? new DateTime(t, DateTimeKind.Utc) : null;
            _restPausedSeconds = row.RestPausedSeconds;
            _restOverShownUtc = null;
            IsRestOver = false;

            if (_restEndsUtc is DateTime ends && ends > DateTime.UtcNow)
            {
                var left = (int)Math.Ceiling((ends - DateTime.UtcNow).TotalSeconds);
                _restTotalSeconds = Math.Max(_restTotalSeconds, left);
                _alarm.Schedule(ends, string.IsNullOrEmpty(_nextUpHint) ? "Time for the next set." : $"Next set: {_nextUpHint}");
            }
            else
            {
                _alarm.Cancel();
            }
            Tick();
        }

        /// <summary>Reloads the cards after a structural change (swap, sets).</summary>
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
            TickRest();
        }

        private void TickRest()
        {
            if (_restPausedSeconds is int paused)
            {
                IsResting = true;
                RestCaption = "PAUSED";
                PauseText = "RESUME";
                RestText = Format(paused);
                RestProgress = 1.0 - (double)paused / _restTotalSeconds;
                return;
            }

            PauseText = "PAUSE";
            if (_restEndsUtc is not DateTime ends)
            {
                IsResting = false;
                IsRestOver = false;
                return;
            }

            var left = (ends - DateTime.UtcNow).TotalSeconds;
            if (left > 0)
            {
                IsResting = true;
                IsRestOver = false;
                RestCaption = "REST";
                RestText = Format((int)Math.Ceiling(left));
                RestProgress = Math.Clamp(1.0 - left / _restTotalSeconds, 0, 1);
                return;
            }

            // Rest just ended. The OS notification vibrates when allowed; otherwise buzz in-app.
            if (_restOverShownUtc is null)
            {
                _restOverShownUtc = DateTime.UtcNow;
                if (!_alarm.CanNotify)
                {
                    try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(600)); } catch { /* no vibrator */ }
                }
            }
            IsResting = true;
            IsRestOver = true;
            RestCaption = "REST OVER";
            RestText = "GO";
            RestProgress = 1;

            // Clear the banner after a few seconds.
            if ((DateTime.UtcNow - _restOverShownUtc.Value).TotalSeconds > 8)
            {
                _restEndsUtc = null;
                IsResting = false;
                IsRestOver = false;
            }
        }

        private static string Format(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private async Task AddExerciseAsync()
        {
            var doable = Library
                .Where(e => e.Category == ExerciseCategory.Strength && ExerciseSwap.IsAvailable(e, AvailableEquipment))
                .Select(e => e.Name).ToArray();
            var pick = await Shell.Current.DisplayActionSheetAsync("Add exercise", "Cancel", null, doable);
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
            if (!await Shell.Current.DisplayAlertAsync("Finish session", message, "Finish", "Keep going")) return;

            StopClock();
            _alarm.Cancel();
            var snap = await _training.FinishAsync(SessionId);
            App.SyncStrava(); // post it to Strava when connected
            App.SyncGuild();  // and tell the guild a training day happened
            Ended?.Invoke(this, snap);
        }

        private async Task DiscardAsync()
        {
            if (!await Shell.Current.DisplayAlertAsync("Discard session",
                    "Throw this session away? Nothing from it will count.", "Discard", "Keep it")) return;
            StopClock();
            _alarm.Cancel();
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
        private Suggestion? _suggestion;

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

        private string? _nextText;
        /// <summary>The progression suggestion for today, e.g. "Try 140 × 6 — all 3 sets hit 10 last time".</summary>
        public string? NextText { get => _nextText; private set => SetProperty(ref _nextText, value); }
        public bool HasNext => NextText is not null;

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
        /// <summary>The loads this exercise can actually use here: owned dumbbells, bells, bar range.</summary>
        private LoadSet Loads => _owner.Gear.For(_snap.Info?.Modality ?? Modality.Dumbbell, Unit, _snap.Info?.NeedsPair ?? false);
        private double LoadLb => WeightMath.ToStoredPounds(Load, Unit);
        private void SetLoadLb(double lb) => Load = Math.Round(WeightMath.ToDisplay(lb, Unit), 2);

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

            // +/- step through weights you actually have: 35 -> 45 if there are no 40s.
            LoadUpCommand = new Command(() => { if (Loads.Next(LoadLb) is double up) SetLoadLb(up); });
            LoadDownCommand = new Command(() => SetLoadLb(Loads.Prev(LoadLb) ?? 0));
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

            var history = await _owner.Training.GetRecentWorkingSetsAsync(Name, _owner.SessionId);
            _suggestion = Progression.Suggest(RxOf(_snap.Row), history, Loads, Unit);
            NextText = _suggestion is null ? null : $"▲ {DescribeTarget(_suggestion)} — {_suggestion.Reason}";
            OnPropertyChanged(nameof(HasNext));
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

            // Within a session, repeat the previous set. Otherwise the progression suggestion
            // (PBI 8.5), then last time's matching set, then the prescription.
            var loadLb = lastHere?.LoadLb ?? _suggestion?.LoadLb ?? lastTime?.LoadLb ?? 0;
            SetLoadLb(loadLb > 0 ? Loads.Snap(loadLb) : 0);
            Reps = lastHere?.Reps ?? _suggestion?.Reps ?? lastTime?.Reps ?? _snap.Row.RepsMin ?? 8;
            Seconds = lastHere?.Seconds ?? _suggestion?.Seconds ?? lastTime?.Seconds ?? _snap.Row.TargetSeconds ?? 30;
        }

        internal static Prescription RxOf(SessionExerciseRow r)
            => new(r.TargetSets ?? 3, r.RepsMin, r.RepsMax, r.TargetSeconds);

        private string DescribeTarget(Suggestion s)
        {
            var load = s.LoadLb > 0 ? $"{LoadDisplay.Label(s.LoadLb)} × " : "";
            return s.Seconds is int sec ? $"{load}{sec}s" : $"{load}{s.Reps}";
        }

        private string Describe(SessionSetRow s)
        {
            var load = s.LoadLb > 0 ? LoadDisplay.Short(s.LoadLb) : "BW";
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
                    LoadText = s.LoadLb > 0 ? LoadDisplay.Label(s.LoadLb) : "bodyweight",
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
            var wasWarmup = IsWarmup;
            await _owner.Training.LogSetAsync(Id, loadLb, IsTimed ? null : Reps, IsTimed ? Seconds : null, IsWarmup);
            IsWarmup = false; // warm-up is a per-set choice, never sticky
            await _owner.ReloadAsync();
            await _owner.StartRestAsync(_snap.Row.RestSeconds, wasWarmup, Name);
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* not all devices */ }
        }

        private async Task DeleteSetAsync(int setId)
        {
            if (!await Shell.Current.DisplayAlertAsync("Delete set", "Remove this set?", "Delete", "Cancel")) return;
            await _owner.Training.DeleteSetAsync(setId);
            await _owner.ReloadAsync();
        }

        private async Task MenuAsync()
        {
            const string swap = "Swap exercise", skip = "Skip", unskip = "Un-skip", up = "Move up", down = "Move down", hist = "History";
            var choice = await Shell.Current.DisplayActionSheetAsync(Name, "Cancel", null, swap, IsSkipped ? unskip : skip, up, down, hist);
            switch (choice)
            {
                case hist: await Forge.Views.ExerciseHistoryPage.OpenAsync(Name); break;
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

            var pick = await Shell.Current.DisplayActionSheetAsync($"Swap {Name}", "Cancel", null, options.ToArray());
            if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
            if (pick == any)
            {
                var all = _owner.Library
                    .Where(e => e.Category == ExerciseCategory.Strength && ExerciseSwap.IsAvailable(e, _owner.AvailableEquipment) && e.Name != Name)
                    .Select(e => e.Name).ToArray();
                pick = await Shell.Current.DisplayActionSheetAsync("Swap to", "Cancel", null, all);
                if (string.IsNullOrEmpty(pick) || pick == "Cancel") return;
            }
            if (pick.StartsWith("↩ ", StringComparison.Ordinal)) pick = pick[2..];

            await _owner.Training.SwapExerciseAsync(Id, pick);
            await _owner.ReloadAsync();
        }
    }
}
