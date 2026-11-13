using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
using Forge.Models;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.ViewModels.Controls.Cards;
using Forge.Views;
using Forge.Views.SubPages;

namespace Forge.ViewModels
{
    /// <summary>
    /// The Today tab (Decision 20): the one screen a daily user opens. One big action, the week at a
    /// glance, active rest, weekly conditioning and a level strip.
    /// </summary>
    public sealed class TodayViewModel : BaseViewModel
    {
        private readonly IProgramService _programs;
        private readonly ITrainingService _training;
        private readonly IActiveRestService _activeRest;
        private readonly IConditioningWeekService _conditioning;
        private readonly IStatsService _stats;
        private readonly IExerciseLibraryImporter _importer;
        private readonly ProgressService _progress;
        private readonly IStatTestService _tests;
        private int? _inProgressId;
        private bool _inProgressGuided;
        private DateOnly _today;

        public event EventHandler<int>? XpAwarded;

        /// <summary>Raised after every refresh, so the code-built map can redraw.</summary>
        public event EventHandler? Refreshed;

        // --- the map (plan phase 4) ---
        public IReadOnlyList<MapNode> Nodes { get; private set; } = [];
        public ProgramDefinition? Quest { get; private set; }
        public int CurrentWeek { get; private set; } = 1;
        public bool TrainingDoneToday { get; private set; }
        public bool BlockComplete { get; private set; }
        public string? InProgressTitle { get; private set; }
        public int StreakDays { get; private set; }
        public double Multiplier { get; private set; } = 1.0;
        public IReadOnlyList<string> ActiveRestMoves { get; private set; } = [];
        public IReadOnlyList<string> ActiveRestAlternatives { get; private set; } = [];

        // --- level strip ---
        private int _level = 1;
        public int Level { get => _level; private set { if (SetProperty(ref _level, value)) OnPropertyChanged(nameof(LevelText)); } }
        public string LevelText => $"LV {Level}";
        private string _xpText = "";
        public string XpText { get => _xpText; private set => SetProperty(ref _xpText, value); }
        private double _xpProgress;
        public double XpProgress { get => _xpProgress; private set => SetProperty(ref _xpProgress, value); }

        private string _spriteFile = UserSettings.HeroSpriteFile;
        public string SpriteFile { get => _spriteFile; private set => SetProperty(ref _spriteFile, value); }

        // --- header ---
        private string _dateText = "";
        public string DateText { get => _dateText; private set => SetProperty(ref _dateText, value); }
        private string _questLine = "";
        public string QuestLine { get => _questLine; private set => SetProperty(ref _questLine, value); }
        private string _pips = "";
        /// <summary>This week's training days: ■ done, □ to do.</summary>
        public string Pips { get => _pips; private set => SetProperty(ref _pips, value); }
        private string _pipsText = "";
        public string PipsText { get => _pipsText; private set => SetProperty(ref _pipsText, value); }

        // --- the one action ---
        private string _actionText = "";
        public string ActionText { get => _actionText; private set => SetProperty(ref _actionText, value); }
        private bool _hasAction;
        public bool HasAction { get => _hasAction; private set => SetProperty(ref _hasAction, value); }
        private string _statusText = "";
        /// <summary>Shown instead of the action when today needs nothing more ("Upper A done today").</summary>
        public string StatusText { get => _statusText; private set { if (SetProperty(ref _statusText, value)) OnPropertyChanged(nameof(HasStatus)); } }
        public bool HasStatus => !string.IsNullOrEmpty(StatusText);
        private string _planText = "";
        /// <summary>Today's exercises.</summary>
        public string PlanText { get => _planText; private set { if (SetProperty(ref _planText, value)) OnPropertyChanged(nameof(HasPlan)); } }
        public bool HasPlan => !string.IsNullOrEmpty(PlanText);
        private bool _canStartOpen;
        public bool CanStartOpen { get => _canStartOpen; private set => SetProperty(ref _canStartOpen, value); }
        private Func<Task> _action = () => Task.CompletedTask;

        // --- active rest ---
        private bool _showActiveRest;
        public bool ShowActiveRest { get => _showActiveRest; private set => SetProperty(ref _showActiveRest, value); }
        private string _activeRestTitle = "Active rest";
        public string ActiveRestTitle { get => _activeRestTitle; private set => SetProperty(ref _activeRestTitle, value); }
        private string _activeRestText = "";
        public string ActiveRestText { get => _activeRestText; private set => SetProperty(ref _activeRestText, value); }
        private bool _activeRestDone;
        public bool ActiveRestDone
        {
            get => _activeRestDone;
            private set { if (SetProperty(ref _activeRestDone, value)) OnPropertyChanged(nameof(ActiveRestButton)); }
        }
        public string ActiveRestButton => ActiveRestDone ? "Done ✓  (tap to undo)" : "Mark active rest done";

        private string _streakText = "";
        public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

        public WeeklyConditioningCardViewModel Conditioning { get; } = new();

        // --- a stat test on its slotted day (Decision 23) ---
        private DueTest? _due;
        private bool _hasTest;
        public bool HasTest { get => _hasTest; private set => SetProperty(ref _hasTest, value); }
        private string _testTitle = "";
        public string TestTitle { get => _testTitle; private set => SetProperty(ref _testTitle, value); }
        private string _testText = "";
        public string TestText { get => _testText; private set => SetProperty(ref _testText, value); }
        public ICommand TestCommand { get; }

        public ICommand ActionCommand { get; }
        public ICommand StartOpenCommand { get; }
        public ICommand ToggleActiveRestCommand { get; }
        public ICommand OpenHeroCommand { get; } = new Command(async () => await Shell.Current.GoToAsync("//hero"));

        public TodayViewModel(IProgramService programs, ITrainingService training, IActiveRestService activeRest,
            IConditioningWeekService conditioning, IStatsService stats, IExerciseLibraryImporter importer, ProgressService progress,
            IStatTestService tests, ISideQuestService sideQuests)
        {
            _tests = tests;
            _sideQuests = sideQuests;
            TestCommand = new Command(async () =>
            {
                if (_due is { } d)
                    await Shell.Current.GoToAsync($"{nameof(StatTestPage)}?kind={d.Kind}&calibration={d.Calibration}");
            });
            _programs = programs;
            _training = training;
            _activeRest = activeRest;
            _conditioning = conditioning;
            _stats = stats;
            _importer = importer;
            _progress = progress;
            Title = "Today";
            ActionCommand = new Command(async () => await _action());
            StartOpenCommand = new Command(async () => await StartAsync(fromProgram: false));
            ToggleActiveRestCommand = new Command(async () => await ToggleActiveRestAsync());
            Conditioning.Title = "Conditioning this week";
            Conditioning.HintText = "Minutes of cardio: bike, bag rounds, brisk walks.";
            Conditioning.ShowHint = true;
            Conditioning.AddMinutesCommand = new Command<object>(async p => await AddMinutesAsync(p));
        }

        // --- side quests (user, 2026-11-13) ---
        private readonly ISideQuestService _sideQuests;

        /// <summary>Today's side quest, if one was logged.</summary>
        public SideQuestRow? TodaySideQuest { get; private set; }

        /// <summary>Yesterday was a big, hard side quest: today's card suggests rest (never enforced).</summary>
        public bool BigDayYesterday { get; private set; }

        public Task<IReadOnlyList<SideQuestActivity>> SideQuestActivitiesAsync() => _sideQuests.RecentAsync();
        public Task<bool> SideQuestCanStandInAsync() => _sideQuests.CanStandInAsync(_today);
        public Task<SideQuestRow?> SideQuestOnAsync(DateOnly date) => _sideQuests.GetAsync(date);

        public async Task LogSideQuestAsync(string activity, int minutes, Effort effort, SideQuestCounts counts, string? note)
        {
            var delta = await _sideQuests.LogAsync(_today, activity, minutes, effort, counts, note);
            await RefreshAsync();
            if (delta > 0) XpAwarded?.Invoke(this, delta);
        }

        public async Task UndoSideQuestAsync()
        {
            await _sideQuests.UndoAsync(_today);
            await RefreshAsync();
        }

        /// <summary>The guided rest-day flow, in the workout screen.</summary>
        public async Task StartActiveRestAsync()
        {
            var snap = await _training.StartActiveRestAsync(_today);
            await Shell.Current.GoToAsync($"{nameof(GuidedSessionPage)}?sessionId={snap.Session.Id}");
        }

        /// <summary>Per-side and loaded flags for timing a day (Decision 28).</summary>
        public Func<string, MoveTraits> Traits { get; private set; } = _ => default;

        public async Task RefreshAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _importer.EnsureSeededAsync(GameConstants.Exercises.LibraryFiles, GameConstants.Exercises.LibraryVersion);
                Traits = await GuidedSupport.TraitsAsync(_training);
                _today = DateOnly.FromDateTime(DateTime.Today);
                DateText = _today.ToString("dddd, MMM d", CultureInfo.CurrentCulture);
                SpriteFile = UserSettings.HeroSpriteFile;

                await _stats.InitAsync();
                var xp = (await _stats.GetUserStatsAsync()).Xp;
                Level = GameMath.LevelFromXp(xp);
                var into = GameMath.XpIntoLevel(xp);
                XpText = $"{into} / {GameMath.XpPerLevel} XP";
                XpProgress = GameMath.LevelProgress(into);

                var view = await _programs.GetViewAsync(_today);
                var open = await _training.GetInProgressAsync();
                _inProgressId = open?.Session.Id;
                _inProgressGuided = open?.Session.ProgramId is not null; // program sessions run guided
                InProgressTitle = open?.Session.Title;
                Nodes = await _programs.GetMapAsync(_today);
                Quest = view?.Program.Definition;
                CurrentWeek = view?.Week ?? 1;
                TrainingDoneToday = view?.CompletedOnDate ?? false;
                BlockComplete = view?.BlockComplete ?? false;
                Multiplier = await _programs.MultiplierForAsync(_today);
                CanStartOpen = open is null;
                _planDay = false;
                ApplyQuest(view, open?.Session.Title);
                if (_planDay) await ShowSwappedPlanAsync();

                _due = await _tests.DueTodayAsync(_today);
                HasTest = _due is not null;
                if (_due is { } due)
                {
                    var (name, what) = due.Kind switch
                    {
                        Forge.Models.StatKind.Strength => ("Strength test", "After your session: one all-out set on three lifts, and push-ups to failure."),
                        Forge.Models.StatKind.Dexterity => ("Mobility test", "Three tape-measure tests: ankle, hamstrings, shoulders. About 10 minutes."),
                        _ => ("Conditioning test", "Three minutes on a step to a beat, then a one-minute pulse count.")
                    };
                    TestTitle = due.Calibration ? $"{name} · practice run" : $"{name} · Honing";
                    TestText = due.Calibration ? $"{what} Unscored: it teaches you the test so your baseline is honest." : what;
                }

                ActiveRestDone = await _activeRest.IsDoneAsync(_today);
                TodaySideQuest = await _sideQuests.GetAsync(_today);
                var yesterday = await _sideQuests.GetAsync(_today.AddDays(-1));
                BigDayYesterday = yesterday is not null && SideQuests.BigDay(yesterday.Minutes, (Effort)yesterday.Effort);
                // Active rest is for days without training: hidden once today's training is done.
                ShowActiveRest = view is null || !view.CompletedOnDate;
                if (view?.Program.Definition.ActiveRest is { } rest)
                {
                    ActiveRestTitle = rest.Name;
                    ActiveRestText = string.Join("\n", rest.Flow.Select(Describe))
                        + "\nOr: " + string.Join(" · ", rest.Alternatives);
                    ActiveRestMoves = rest.Flow.Select(Describe).ToList();
                    ActiveRestAlternatives = rest.Alternatives.ToList();
                }

                var week = await _conditioning.GetWeekProgressAsync(_today);
                Conditioning.Minutes = week.Minutes;
                Conditioning.GoalMinutes = week.GoalMinutes;
                Conditioning.WeekRangeText = $"{week.WeekStart:MMM d} – {week.WeekEnd:MMM d}";

                var (current, longest) = await _progress.GetStreakAsync();
                StreakDays = current;
                var movement = current switch
                {
                    0 => "No streak yet: train or do active rest to start one.",
                    1 => $"Moved 1 day in a row · best {Math.Max(1, longest)}",
                    _ => $"Moved {current} days in a row · best {longest}"
                };
                StreakText = view is { Multiplier: > 1.0 }
                    ? $"{movement}\nStreak bonus: ×{view.Multiplier:0.0} XP today"
                    : movement;
            }
            finally
            {
                IsBusy = false;
            }
            Refreshed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Starts today's session from the map: the briefing, or resumes one already running.</summary>
        public Task StartTodayAsync() => _inProgressId is not null
            ? OpenSessionAsync(_inProgressId, _inProgressGuided)
            : Shell.Current.GoToAsync(nameof(BriefingPage));

        public Task<SessionPlan> PlanDayAsync(int dayIndex, int week) => _training.PlanForDayAsync(dayIndex, week, UserSettings.LastLocation);

        public Task ToggleRestAsync() => ToggleActiveRestAsync();

        public Task AddCardioAsync(int minutes) => AddMinutesAsync(minutes);

        public Task StartOpenSessionAsync() => StartAsync(fromProgram: false);

        public Task TakeTestAsync() => _due is { } d
            ? Shell.Current.GoToAsync($"{nameof(StatTestPage)}?kind={d.Kind}&calibration={d.Calibration}")
            : Task.CompletedTask;

        private void ApplyQuest(BlockView? view, string? inProgressTitle)
        {
            StatusText = "";
            PlanText = "";
            HasAction = true;

            if (inProgressTitle is not null)
            {
                ActionText = $"Resume: {inProgressTitle}";
                _action = () => OpenSessionAsync(_inProgressId, _inProgressGuided);
            }

            if (view is null)
            {
                QuestLine = "No quest accepted";
                Pips = PipsText = "";
                if (inProgressTitle is null)
                {
                    ActionText = "Choose a quest";
                    _action = () => Shell.Current.GoToAsync(nameof(QuestPage));
                }
                return;
            }

            var def = view.Program.Definition;
            QuestLine = view.BlockComplete
                ? $"{def.Name} · complete"
                : $"{def.Name} · week {view.Week} of {ProgramDefinition.WeeksPerBlock}: {ProgramDefinition.WeekName(view.Week)}";
            var done = view.DoneThisWeek.Count;
            Pips = string.Join(" ", Enumerable.Range(0, view.DayCount).Select(i => view.DoneThisWeek.Contains(i) ? "■" : "□"));
            PipsText = $"{done} of {view.DayCount} training days this week";

            if (inProgressTitle is not null) return;

            if (view.BlockComplete)
            {
                ActionText = "Choose your next quest";
                _action = () => Shell.Current.GoToAsync(nameof(QuestPage));
            }
            else if (view.CompletedOnDate)
            {
                HasAction = false;
                StatusText = $"{view.Day?.Name ?? "Training"} done today. Rest well.";
            }
            else if (view.Day is { } day)
            {
                ActionText = $"Start {day.Name}" + (day.IsLight ? " (light)" : "");
                _action = () => Shell.Current.GoToAsync(nameof(BriefingPage));
                PlanText = string.Join("\n", day.Slots.Select(r => Describe(r.Slot) + (r.IsMain ? "" : "  · acc")));
                _planDay = true;
            }
            else
            {
                HasAction = false;
                StatusText = "This week's training is done. Active rest until the week turns.";
            }
        }

        private bool _planDay;

        /// <summary>
        /// Today's exercises as they'll actually be done where you last trained: the briefing's plan,
        /// with home swaps applied (Floor Press, not Bench Press, without a bench).
        /// </summary>
        private async Task ShowSwappedPlanAsync()
        {
            try
            {
                var plan = await _training.PlanFromProgramAsync(_today, UserSettings.LastLocation);
                PlanText = string.Join("\n", plan.Exercises.Select(p => Describe(p.Slot with { Exercise = p.Name }) + (p.IsMain ? "" : "  · acc")));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plan preview fell back to the prescription: {ex.Message}");
            }
        }

        internal static string Describe(ProgramSlot s)
        {
            var per = s.IsTimed ? $"{s.Seconds}s"
                : s.RepsMin == s.RepsMax ? $"{s.RepsMin}" : $"{s.RepsMin}–{s.RepsMax}";
            return $"{s.Exercise}  {s.Sets} × {per}";
        }

        private async Task ToggleActiveRestAsync()
        {
            var delta = await _activeRest.SetDoneAsync(_today, !ActiveRestDone);
            await RefreshAsync();
            if (delta > 0) XpAwarded?.Invoke(this, delta);
        }

        private async Task AddMinutesAsync(object? p)
        {
            if (p is null || !int.TryParse(p.ToString(), out var m) || m <= 0) return;
            var wasComplete = Conditioning.GoalMinutes > 0 && Conditioning.Minutes >= Conditioning.GoalMinutes;
            var updated = await _conditioning.AddConditioningMinutesAsync(_today, m);
            Conditioning.Minutes = updated.Minutes;
            Conditioning.GoalMinutes = updated.GoalMinutes;
            if (!wasComplete && updated.Completed)
                await Shell.Current.DisplayAlertAsync("Weekly goal reached",
                    $"{updated.Minutes} conditioning minutes this week. +{GameMath.Quests.ConditioningWeeklyXp} XP", "Nice");
        }

        private async Task StartAsync(bool fromProgram)
        {
            // Location decides equipment, and so which swaps happen. Last-used location first.
            var last = UserSettings.LastLocation;
            var first = last == TrainingLocation.Gym ? "Gym" : "Home";
            var second = last == TrainingLocation.Gym ? "Home" : "Gym";
            var pick = await Shell.Current.DisplayActionSheetAsync("Where are you training?", "Cancel", null, first, second);
            if (pick is not ("Gym" or "Home")) return;
            var location = pick == "Gym" ? TrainingLocation.Gym : TrainingLocation.Home;

            if (location == TrainingLocation.Home && await _training.GetEquipmentAsync(location) == Forge.Models.Equipment.None)
            {
                var setUp = await Shell.Current.DisplayAlertAsync("No home gear set up",
                    "Forge doesn't know what equipment you have at home, so every lift will swap to a bodyweight move. Set up your gear first?",
                    "Set up gear", "Start anyway");
                if (setUp)
                {
                    await Shell.Current.GoToAsync(nameof(MyGearPage));
                    return;
                }
            }

            var snap = fromProgram
                ? await _training.StartFromProgramAsync(_today, location)
                : await _training.StartEmptyAsync(_today, location);
            await OpenSessionAsync(snap.Session.Id);
        }

        private static async Task OpenSessionAsync(int? sessionId, bool guided = false)
        {
            if (sessionId is null) return;
            await Shell.Current.GoToAsync($"{(guided ? nameof(GuidedSessionPage) : nameof(ActiveSessionPage))}?sessionId={sessionId}");
        }
    }
}
