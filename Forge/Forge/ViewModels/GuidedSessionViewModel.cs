using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.Views;

namespace Forge.ViewModels
{
    public enum GuidedPhase { Loading, Ready, Work, Rest, Done }

    /// <summary>
    /// The guided session: one set at a time, hands-free. A set runs on a timer at a steady pace
    /// (the user chose timed sets over tapping), logs its target when the timer ends, and rest
    /// starts. Near the end of rest it's "Get ready!" with a 3-2-1 buzz, and the next set appears
    /// on its own. Misses are corrected on the rest screen. Rest is persisted (it survives the app
    /// being killed); a set in progress is not — on return the set waits for a tap.
    /// </summary>
    public sealed class GuidedSessionViewModel : BaseViewModel
    {
        private const int GetReadySeconds = 10;
        private const int LeadInSeconds = 10;

        private readonly ITrainingService _training;
        private readonly IRestAlarm _alarm;
        private readonly IStatTestService _tests;
        private IDispatcherTimer? _timer;

        private SessionSnapshot? _snap;
        private List<SessionExerciseSnapshot> _exercises = new();
        private IReadOnlyList<GuidedStep> _steps = Array.Empty<GuidedStep>();
        private int _pos;
        private GearLoads _gear = new();

        private DateTime? _workEndsUtc;
        private int? _workPausedSeconds;
        private int _workTotal;
        private DateTime? _restEndsUtc;
        private int? _restPausedSeconds;
        private int _restTotal;
        private int _lastBuzz = -1;
        private bool _logging;

        private SessionSetRow? _lastSet;
        private SessionExerciseSnapshot? _lastSetExercise;

        public int SessionId { get; private set; }
        public event EventHandler<SessionSnapshot?>? Ended;

        // --- phase ---
        private GuidedPhase _phase = GuidedPhase.Loading;
        public GuidedPhase Phase
        {
            get => _phase;
            private set
            {
                if (!SetProperty(ref _phase, value)) return;
                foreach (var n in new[] { nameof(IsReady), nameof(IsWork), nameof(IsRest), nameof(IsDone), nameof(IsActive) })
                    OnPropertyChanged(n);
            }
        }
        public bool IsReady => Phase == GuidedPhase.Ready;
        public bool IsWork => Phase == GuidedPhase.Work;
        public bool IsRest => Phase == GuidedPhase.Rest;
        public bool IsDone => Phase == GuidedPhase.Done;
        public bool IsActive => Phase is GuidedPhase.Work or GuidedPhase.Rest;

        // --- header ---
        private string _stepText = "";
        public string StepText { get => _stepText; private set => SetProperty(ref _stepText, value); }
        private string _elapsedText = "0:00";
        public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

        // --- the set (current while working, next while resting) ---
        private string _caption = "";
        public string Caption { get => _caption; private set => SetProperty(ref _caption, value); }
        private string _exerciseName = "";
        public string ExerciseName { get => _exerciseName; private set => SetProperty(ref _exerciseName, value); }
        private string _loadText = "";
        public string LoadText { get => _loadText; private set => SetProperty(ref _loadText, value); }
        private string _workText = "";
        public string WorkText { get => _workText; private set => SetProperty(ref _workText, value); }
        private string _thenText = "";
        public string ThenText { get => _thenText; private set => SetProperty(ref _thenText, value); }

        // --- countdowns ---
        private string _clockText = "";
        public string ClockText { get => _clockText; private set => SetProperty(ref _clockText, value); }
        private double _clockProgress;
        public double ClockProgress { get => _clockProgress; private set => SetProperty(ref _clockProgress, value); }
        private bool _isGetReady;
        public bool IsGetReady { get => _isGetReady; private set => SetProperty(ref _isGetReady, value); }
        private bool _isPaused;
        public bool IsPaused { get => _isPaused; private set { if (SetProperty(ref _isPaused, value)) OnPropertyChanged(nameof(PauseText)); } }
        public string PauseText => IsPaused ? "Resume" : "Pause";

        // --- up next (rest) ---
        private string _nextText = "";
        public string NextText { get => _nextText; private set => SetProperty(ref _nextText, value); }

        // --- the set just logged (rest), correctable ---
        private string _loggedText = "";
        public string LoggedText { get => _loggedText; private set => SetProperty(ref _loggedText, value); }
        public bool HasLogged => _lastSet is not null;

        // --- "Got all 8?" after a hard set (unanswered = yes) ---
        private bool _isAsking;
        public bool IsAsking { get => _isAsking; private set { if (SetProperty(ref _isAsking, value)) OnPropertyChanged(nameof(ShowLogged)); } }
        public bool ShowLogged => HasLogged && !IsAsking;
        private string _askText = "";
        public string AskText { get => _askText; private set => SetProperty(ref _askText, value); }
        public ICommand AnswerYesCommand { get; }
        public ICommand AnswerShortCommand { get; }

        private string _readyText = "";
        public string ReadyText { get => _readyText; private set => SetProperty(ref _readyText, value); }

        // --- a stat test as the last steps of a test day (Decision 23) ---
        private DueTest? _dueTest;
        private bool _hasTest;
        public bool HasTest { get => _hasTest; private set => SetProperty(ref _hasTest, value); }
        private string _testText = "";
        public string TestText { get => _testText; private set => SetProperty(ref _testText, value); }
        private string _testButton = "";
        public string TestButton { get => _testButton; private set => SetProperty(ref _testButton, value); }
        public ICommand TestCommand { get; }

        private string _doneText = "";
        public string DoneText { get => _doneText; private set => SetProperty(ref _doneText, value); }

        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand MoreTimeCommand { get; }
        public ICommand LessTimeCommand { get; }
        public ICommand SkipCommand { get; }
        public ICommand LoggedRepsUpCommand { get; }
        public ICommand LoggedRepsDownCommand { get; }
        public ICommand LoggedLoadUpCommand { get; }
        public ICommand LoggedLoadDownCommand { get; }
        public ICommand OverviewCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand MenuCommand { get; }

        public GuidedSessionViewModel(ITrainingService training, IRestAlarm alarm, IStatTestService tests)
        {
            _training = training;
            _alarm = alarm;
            _tests = tests;
            TestCommand = new Command(async () =>
            {
                if (_dueTest is { } d)
                    await Shell.Current.GoToAsync($"{nameof(Forge.Views.SubPages.StatTestPage)}?kind={d.Kind}&calibration={d.Calibration}");
            });
            StartCommand = new Command(async () => await StartWorkAsync());
            PauseCommand = new Command(async () => await TogglePauseAsync());
            MoreTimeCommand = new Command(async () => await AdjustAsync(+1));
            LessTimeCommand = new Command(async () => await AdjustAsync(-1));
            SkipCommand = new Command(async () => await SkipAsync());
            LoggedRepsUpCommand = new Command(async () => await EditLoggedAsync(reps: +1));
            LoggedRepsDownCommand = new Command(async () => await EditLoggedAsync(reps: -1));
            LoggedLoadUpCommand = new Command(async () => await EditLoggedAsync(load: +1));
            LoggedLoadDownCommand = new Command(async () => await EditLoggedAsync(load: -1));
            AnswerYesCommand = new Command(() => IsAsking = false);
            AnswerShortCommand = new Command<string>(async n =>
            {
                IsAsking = false;
                if (int.TryParse(n, out var by)) await EditLoggedAsync(reps: -by);
            });
            OverviewCommand = new Command(async () =>
                await Shell.Current.GoToAsync($"{nameof(ActiveSessionPage)}?sessionId={SessionId}"));
            FinishCommand = new Command(async () => await FinishAsync());
            MenuCommand = new Command(async () => await MenuAsync());
        }

        // ------------------------------------------------------------------ loading

        public async Task LoadAsync(int sessionId)
        {
            SessionId = sessionId;
            await ReloadAsync();
            // Finished or discarded from the list view: nothing left to guide.
            if (_snap!.Session.EndedAtUtc is not null) { Ended?.Invoke(this, null); return; }
            try { await _alarm.EnsurePermissionAsync(); } catch { /* permission UI unavailable */ }

            var nothingLogged = _snap!.Exercises.All(e => e.Sets.Count == 0);
            if (_pos >= _steps.Count) ShowDone();
            else if (_snap.Session.RestEndsAtUtc is not null || _snap.Session.RestPausedSeconds is not null) EnterRest(_snap.Session);
            else if (nothingLogged)
                EnterRest(await _training.StartRestAsync(SessionId, LeadInSeconds), LeadInSeconds); // a lead-in to the first set
            else ShowReady();
        }

        /// <summary>Re-reads the session (the overview may have logged, skipped or swapped).</summary>
        private async Task ReloadAsync()
        {
            _snap = await _training.GetSessionAsync(SessionId);
            _gear = UserSettings.GearAt(_snap.Location);
            foreach (var e in _snap.Exercises.Where(e => e.Row.PlannedLoadLb is null && !_historyLoad.ContainsKey(e.Row.ExerciseName)))
            {
                var last = (await _training.GetRecentWorkingSetsAsync(e.Row.ExerciseName, SessionId)).FirstOrDefault(h => h.Count > 0);
                _historyLoad[e.Row.ExerciseName] = last?.Max(w => w.LoadLb) ?? 0;
            }
            Title = _snap.Session.Title ?? "Session";
            _exercises = _snap.Exercises.Where(e => !e.Row.Skipped).OrderBy(e => e.Row.Position).ToList();
            _steps = GuidedPlan.Build(_exercises.Select((e, i) => new GuidedExercise(i, e.Row.Superset, e.Row.TargetSets ?? 3,
                e.Row.RestSeconds, WarmupsFor(e).Count)).ToList(),
                UserSettings.DefaultRestSeconds);
            _pos = GuidedPlan.Position(_steps, i => (_exercises[i].Sets.Count(s => s.IsWarmup), _exercises[i].Sets.Count(s => !s.IsWarmup)));
            StepText = $"Set {Math.Min(_pos + 1, _steps.Count)} of {_steps.Count}";
        }

        // ------------------------------------------------------------------ targets

        /// <summary>Briefed weight; for sessions started without a briefing, last time's top weight.</summary>
        private double PlannedLoad(SessionExerciseSnapshot e)
            => e.Row.PlannedLoadLb ?? _historyLoad.GetValueOrDefault(e.Row.ExerciseName);
        private readonly Dictionary<string, double> _historyLoad = new();

        private IReadOnlyList<WarmupSet> WarmupsFor(SessionExerciseSnapshot e)
            => GuidedSupport.Warmups(e.Row.IsMain, e.Info, e.IsTimed, PlannedLoad(e), LoadsFor(e));

        private LoadSet LoadsFor(SessionExerciseSnapshot e)
            => _gear.For(e.Info?.Modality ?? Modality.Bodyweight, UserSettings.WeightUnit, e.Info?.NeedsPair ?? false);

        /// <summary>Load, reps and seconds for a step. A corrected weight carries forward to later sets.</summary>
        private (double LoadLb, int? Reps, int? Seconds) Target(GuidedStep step)
        {
            var e = _exercises[step.ExerciseIndex];
            var working = e.Sets.Where(s => !s.IsWarmup).Select(s => (double?)s.LoadLb).LastOrDefault() ?? PlannedLoad(e);
            if (step.IsWarmup)
            {
                var w = WarmupsFor(e).ElementAtOrDefault(step.SetNumber - 1);
                return (w?.LoadLb ?? PlannedLoad(e), w?.Reps ?? GuidedPlan.WarmupReps, null);
            }
            return e.IsTimed
                ? (working, null, e.Row.PlannedSeconds ?? e.Row.TargetSeconds ?? 30)
                : (working, e.Row.PlannedReps ?? e.Row.RepsMin ?? 8, null);
        }

        private string Label(double lb, SessionExerciseSnapshot e)
            => lb <= 0 && !GuidedSupport.IsLoaded(e.Info) ? "Bodyweight" : _gear.LoadLabel(lb, UserSettings.WeightUnit);

        private string Describe(GuidedStep step)
        {
            var e = _exercises[step.ExerciseIndex];
            var (lb, reps, secs) = Target(step);
            var set = step.IsWarmup ? $"warm-up {WarmupCaption(step).ToLowerInvariant()}" : $"set {step.SetNumber} of {step.SetsTotal}";
            var work = secs is int s ? $"{s}s" : $"× {reps}";
            return $"{e.Row.ExerciseName} · {set}\n{Label(lb, e)} {work}";
        }

        /// <summary>"1 OF 2 · 50%", or just "· 50%" for a single warm-up.</summary>
        private string WarmupCaption(GuidedStep step)
        {
            var all = WarmupsFor(_exercises[step.ExerciseIndex]);
            var pct = all.ElementAtOrDefault(step.SetNumber - 1) is { } w ? $"· {w.Percent}%" : "";
            return all.Count > 1 ? $"{step.SetNumber} OF {all.Count} {pct}" : pct;
        }

        private void ShowStep(GuidedStep step)
        {
            var e = _exercises[step.ExerciseIndex];
            var (lb, reps, secs) = Target(step);
            var group = GuidedSupport.GroupLabel(_exercises.Select(x => x.Row.Superset).ToList(), step.ExerciseIndex);
            ExerciseName = group is null ? e.Row.ExerciseName : $"{group} · {e.Row.ExerciseName}";
            Caption = step.IsWarmup ? $"WARM-UP {WarmupCaption(step)}" : $"SET {step.SetNumber} OF {step.SetsTotal}";
            LoadText = Label(lb, e);
            WorkText = secs is int s ? $"{s} s" : $"× {reps}";
            StepText = $"Set {_pos + 1} of {_steps.Count}";
        }

        // ------------------------------------------------------------------ phases

        private void ShowReady()
        {
            _workEndsUtc = null;
            IsPaused = false;
            ShowStep(_steps[_pos]);
            ReadyText = "Tap when you're in position";
            Phase = GuidedPhase.Ready;
        }

        private async Task StartWorkAsync()
        {
            if (_pos >= _steps.Count) return;
            await _training.StopRestAsync(SessionId);
            _alarm.Cancel();
            _restEndsUtc = null;
            _restPausedSeconds = null;
            IsGetReady = false;
            IsAsking = false;

            var step = _steps[_pos];
            ShowStep(step);
            var (_, reps, secs) = Target(step);
            _workTotal = GuidedPlan.SetSeconds(reps, secs);
            _workEndsUtc = DateTime.UtcNow.AddSeconds(_workTotal);
            _workPausedSeconds = null;
            IsPaused = false;
            ThenText = step.RestAfterSeconds > 0 && _pos + 1 < _steps.Count
                ? $"then rest {Format(step.RestAfterSeconds)}, then {_exercises[_steps[_pos + 1].ExerciseIndex].Row.ExerciseName}"
                : "last set of the session";
            Phase = GuidedPhase.Work;
            Buzz(150);
        }

        /// <summary>The set's timer ran out: log the target and rest.</summary>
        private async Task CompleteSetAsync()
        {
            if (_logging || _pos >= _steps.Count) return;
            _logging = true;
            try
            {
                var step = _steps[_pos];
                var e = _exercises[step.ExerciseIndex];
                var (lb, reps, secs) = Target(step);
                _lastSet = await _training.LogSetAsync(e.Row.Id, lb, reps, secs, step.IsWarmup);
                // Hard sets ask instead of assuming (unanswered = got them all).
                IsAsking = GuidedPlan.IsHardSet(step, reps, e.Row.RepsMax);
                AskText = $"Got all {reps}?";
                await ReloadAsync();
                _lastSetExercise = _exercises.FirstOrDefault(x => x.Row.Id == e.Row.Id);
                RefreshLogged();

                if (_pos >= _steps.Count)
                {
                    ShowDone();
                    Buzz(600);
                    return;
                }
                var rest = Math.Max(step.RestAfterSeconds, 5);
                EnterRest(await _training.StartRestAsync(SessionId, rest), rest);
            }
            finally { _logging = false; }
        }

        private void EnterRest(TrainingSessionRow row, int? total = null)
        {
            _workEndsUtc = null;
            ApplyRest(row, total);
            var next = _steps[_pos];
            NextText = Describe(next);
            ShowStep(next); // the rest screen shows what's coming, big
            Phase = GuidedPhase.Rest;
            ScheduleAlarm();
        }

        private void ApplyRest(TrainingSessionRow row, int? total = null)
        {
            _restEndsUtc = row.RestEndsAtUtc is long t ? new DateTime(t, DateTimeKind.Utc) : null;
            _restPausedSeconds = row.RestPausedSeconds;
            // The full length for the progress bar: what was asked for, or (after a restart) what's left.
            var left = _restPausedSeconds ?? (_restEndsUtc is DateTime e ? (int)Math.Ceiling((e - DateTime.UtcNow).TotalSeconds) : 0);
            _restTotal = Math.Max(Math.Max(1, left), total ?? 0);
            IsPaused = _restPausedSeconds is not null;
            _lastBuzz = -1;
            Tick();
        }

        private void ScheduleAlarm()
        {
            if (_restEndsUtc is DateTime ends && _pos < _steps.Count)
                _alarm.Schedule(ends, $"Next: {Describe(_steps[_pos]).Replace('\n', ' ')}");
            else _alarm.Cancel();
        }

        private void ShowDone()
        {
            _alarm.Cancel();
            _workEndsUtc = null;
            var working = _exercises.Sum(e => e.Sets.Count(s => !s.IsWarmup));
            DoneText = $"{working} working sets in {ElapsedText}.";
            _ = LoadDueTestAsync();
            StepText = $"{_steps.Count} of {_steps.Count}";
            Phase = GuidedPhase.Done;
        }

        private async Task LoadDueTestAsync()
        {
            _dueTest = await _tests.DueTodayAsync(DateOnly.FromDateTime(DateTime.Today));
            HasTest = _dueTest is not null;
            if (_dueTest is not { } due) return;
            var (name, what) = due.Kind switch
            {
                StatKind.Strength => ("strength test", "one all-out set on your three test lifts, then push-ups to failure. You're warm, so it's the right moment."),
                StatKind.Dexterity => ("mobility test", "three tape-measure tests: ankle, hamstrings, shoulders."),
                _ => ("conditioning test", "three minutes on a step to a beat, then a one-minute pulse count. Take 5 minutes first so your pulse settles.")
            };
            TestText = $"{(due.Calibration ? "Practice run" : "Honing")}: the {name} is next. {char.ToUpperInvariant(what[0])}{what[1..]}";
            TestButton = due.Calibration ? $"Do the practice {name}" : $"Take the {name}";
        }

        // ------------------------------------------------------------------ the clock

        public void StartClock()
        {
            _timer ??= Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(250);
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
            Tick();
        }

        public void StopClock() => _timer?.Stop();

        private void OnTick(object? sender, EventArgs e) => Tick();

        private void Tick()
        {
            if (_snap is not null)
            {
                var elapsed = DateTime.UtcNow - new DateTime(_snap.Session.StartedAtUtc, DateTimeKind.Utc);
                ElapsedText = elapsed.TotalHours >= 1 ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");
            }

            if (Phase == GuidedPhase.Work)
            {
                var left = _workPausedSeconds ?? (int)Math.Ceiling(((_workEndsUtc ?? DateTime.UtcNow) - DateTime.UtcNow).TotalSeconds);
                ClockText = Format(Math.Max(0, left));
                ClockProgress = 1.0 - (double)Math.Max(0, left) / _workTotal;
                if (left <= 0 && _workPausedSeconds is null) _ = CompleteSetAsync();
            }
            else if (Phase == GuidedPhase.Rest)
            {
                var left = _restPausedSeconds ?? (int)Math.Ceiling(((_restEndsUtc ?? DateTime.UtcNow) - DateTime.UtcNow).TotalSeconds);
                ClockText = Format(Math.Max(0, left));
                ClockProgress = 1.0 - (double)Math.Max(0, left) / _restTotal;
                IsGetReady = left <= GetReadySeconds;
                Caption = IsGetReady ? "GET READY!" : "REST";
                // 3-2-1, then go.
                if (_restPausedSeconds is null && left is >= 1 and <= 3 && left != _lastBuzz)
                {
                    _lastBuzz = left;
                    Buzz(120);
                }
                if (left <= 0 && _restPausedSeconds is null) _ = StartWorkAsync();
            }
        }

        // ------------------------------------------------------------------ controls

        private async Task TogglePauseAsync()
        {
            if (Phase == GuidedPhase.Work)
            {
                if (_workPausedSeconds is int left)
                {
                    _workEndsUtc = DateTime.UtcNow.AddSeconds(left);
                    _workPausedSeconds = null;
                    IsPaused = false;
                }
                else
                {
                    _workPausedSeconds = Math.Max(0, (int)Math.Ceiling(((_workEndsUtc ?? DateTime.UtcNow) - DateTime.UtcNow).TotalSeconds));
                    IsPaused = true;
                }
            }
            else if (Phase == GuidedPhase.Rest)
            {
                ApplyRest(await _training.ToggleRestPauseAsync(SessionId), _restTotal);
                ScheduleAlarm();
            }
        }

        /// <summary>More or less time: +10/−10 s on a set, +15/−15 s on a rest.</summary>
        private async Task AdjustAsync(int sign)
        {
            if (Phase == GuidedPhase.Work)
            {
                if (_workPausedSeconds is int p) _workPausedSeconds = Math.Max(1, p + 10 * sign);
                else if (_workEndsUtc is DateTime end) _workEndsUtc = end.AddSeconds(10 * sign);
                _workTotal = Math.Max(1, _workTotal + 10 * sign);
            }
            else if (Phase == GuidedPhase.Rest)
            {
                ApplyRest(await _training.AdjustRestAsync(SessionId, 15 * sign), Math.Max(1, _restTotal + 15 * sign));
                ScheduleAlarm();
            }
        }

        /// <summary>Done early (set) or go now (rest).</summary>
        private async Task SkipAsync()
        {
            if (Phase == GuidedPhase.Work) await CompleteSetAsync();
            else if (Phase == GuidedPhase.Rest) await StartWorkAsync();
        }

        private void RefreshLogged()
        {
            if (_lastSet is null || _lastSetExercise is null) { LoggedText = ""; OnPropertyChanged(nameof(HasLogged)); return; }
            var work = _lastSet.Seconds is int s ? $"{s}s" : $"× {_lastSet.Reps}";
            OnPropertyChanged(nameof(ShowLogged));
            LoggedText = $"Logged {_lastSetExercise.Row.ExerciseName}{(_lastSet.IsWarmup ? " warm-up" : "")}: {Label(_lastSet.LoadLb, _lastSetExercise)} {work}";
            OnPropertyChanged(nameof(HasLogged));
        }

        /// <summary>Fix the set just logged: a rep short, or a different weight (which then carries forward).</summary>
        private async Task EditLoggedAsync(int reps = 0, int load = 0)
        {
            if (_lastSet is null || _lastSetExercise is null) return;
            if (reps != 0)
            {
                if (_lastSet.Seconds is int s) _lastSet.Seconds = Math.Max(0, s + 5 * reps);
                else _lastSet.Reps = Math.Max(0, (_lastSet.Reps ?? 0) + reps);
            }
            if (load != 0)
            {
                var loads = LoadsFor(_lastSetExercise);
                _lastSet.LoadLb = load > 0 ? loads.Next(_lastSet.LoadLb) ?? _lastSet.LoadLb : loads.Prev(_lastSet.LoadLb) ?? 0;
            }
            await _training.UpdateSetAsync(_lastSet.Id, _lastSet.LoadLb, _lastSet.Reps, _lastSet.Seconds);
            await ReloadAsync();
            _lastSetExercise = _exercises.FirstOrDefault(x => x.Row.Id == _lastSetExercise.Row.Id) ?? _lastSetExercise;
            RefreshLogged();
            if (Phase == GuidedPhase.Rest && _pos < _steps.Count)
            {
                NextText = Describe(_steps[_pos]);
                ShowStep(_steps[_pos]);
            }
        }

        private async Task MenuAsync()
        {
            var choice = await Shell.Current.DisplayActionSheetAsync("Session", "Cancel", null,
                "Skip this exercise", "End session now", "Discard session");
            switch (choice)
            {
                case "Skip this exercise" when _pos < _steps.Count:
                    var e = _exercises[_steps[_pos].ExerciseIndex];
                    await _training.SetSkippedAsync(e.Row.Id, true);
                    await ReloadAsync();
                    if (_pos >= _steps.Count) ShowDone();
                    else if (Phase == GuidedPhase.Rest) EnterRest((await _training.GetSessionAsync(SessionId)).Session);
                    else ShowReady();
                    break;
                case "End session now":
                    if (await Shell.Current.DisplayAlertAsync("End session?", "Sets logged so far are kept.", "End", "Keep going"))
                        await FinishAsync();
                    break;
                case "Discard session":
                    if (await Shell.Current.DisplayAlertAsync("Discard session?", "It won't count toward history, progression or the quest.", "Discard", "Keep"))
                    {
                        _alarm.Cancel();
                        await _training.DiscardAsync(SessionId);
                        Ended?.Invoke(this, null);
                    }
                    break;
            }
        }

        private async Task FinishAsync()
        {
            _alarm.Cancel();
            StopClock();
            var finished = await _training.FinishAsync(SessionId);
            App.SyncStrava(); // post it to Strava when connected
            Ended?.Invoke(this, finished);
        }

        // ------------------------------------------------------------------ helpers

        private static string Format(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static void Buzz(int ms)
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(ms)); } catch { /* no vibrator */ }
        }
    }
}
