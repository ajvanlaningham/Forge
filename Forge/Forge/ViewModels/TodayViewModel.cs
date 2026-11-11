using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
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
        private int? _inProgressId;
        private bool _inProgressGuided;
        private DateOnly _today;

        public event EventHandler<int>? XpAwarded;

        // --- level strip ---
        private int _level = 1;
        public int Level { get => _level; private set { if (SetProperty(ref _level, value)) OnPropertyChanged(nameof(LevelText)); } }
        public string LevelText => $"LV {Level}";
        private string _xpText = "";
        public string XpText { get => _xpText; private set => SetProperty(ref _xpText, value); }
        private double _xpProgress;
        public double XpProgress { get => _xpProgress; private set => SetProperty(ref _xpProgress, value); }

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

        public ICommand ActionCommand { get; }
        public ICommand StartOpenCommand { get; }
        public ICommand ToggleActiveRestCommand { get; }
        public ICommand OpenHeroCommand { get; } = new Command(async () => await Shell.Current.GoToAsync("//hero"));

        public TodayViewModel(IProgramService programs, ITrainingService training, IActiveRestService activeRest,
            IConditioningWeekService conditioning, IStatsService stats, IExerciseLibraryImporter importer, ProgressService progress)
        {
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

        public async Task RefreshAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _importer.EnsureSeededAsync(GameConstants.Exercises.LibraryFiles, GameConstants.Exercises.LibraryVersion);
                _today = DateOnly.FromDateTime(DateTime.Today);
                DateText = _today.ToString("dddd, MMM d", CultureInfo.CurrentCulture);

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
                CanStartOpen = open is null;
                ApplyQuest(view, open?.Session.Title);

                ActiveRestDone = await _activeRest.IsDoneAsync(_today);
                // Active rest is for days without training: hidden once today's training is done.
                ShowActiveRest = view is null || !view.CompletedOnDate;
                if (view?.Program.Definition.ActiveRest is { } rest)
                {
                    ActiveRestTitle = rest.Name;
                    ActiveRestText = string.Join("\n", rest.Flow.Select(Describe))
                        + "\nOr: " + string.Join(" · ", rest.Alternatives);
                }

                var week = await _conditioning.GetWeekProgressAsync(_today);
                Conditioning.Minutes = week.Minutes;
                Conditioning.GoalMinutes = week.GoalMinutes;
                Conditioning.WeekRangeText = $"{week.WeekStart:MMM d} – {week.WeekEnd:MMM d}";

                var (current, longest) = await _progress.GetStreakAsync();
                var movement = current switch
                {
                    0 => "No streak yet: train or do active rest to start one.",
                    1 => $"Moved 1 day in a row · best {Math.Max(1, longest)}",
                    _ => $"Moved {current} days in a row · best {longest}"
                };
                StreakText = view is { PerfectWeekStreak: > 0 }
                    ? $"{movement}\nPerfect weeks: {view.PerfectWeekStreak} · ×{view.Multiplier:0.0} XP this week"
                    : movement;
            }
            finally
            {
                IsBusy = false;
            }
        }

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
                    _action = () => Shell.Current.GoToAsync("//quest");
                }
                return;
            }

            var def = view.Program.Definition;
            QuestLine = view.BlockComplete
                ? $"{def.QuestName} · complete"
                : $"{def.QuestName} · week {view.Week} of {ProgramDefinition.WeeksPerBlock}: {ProgramDefinition.WeekName(view.Week)}";
            var done = view.DoneThisWeek.Count;
            Pips = string.Join(" ", Enumerable.Range(0, view.DayCount).Select(i => view.DoneThisWeek.Contains(i) ? "■" : "□"));
            PipsText = $"{done} of {view.DayCount} training days this week";

            if (inProgressTitle is not null) return;

            if (view.BlockComplete)
            {
                ActionText = $"The {def.QuestName} awaits: see the quest";
                _action = () => Shell.Current.GoToAsync("//quest");
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
            }
            else
            {
                HasAction = false;
                StatusText = "This week's training is done. Active rest until the week turns.";
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
                await Shell.Current.DisplayAlert("Weekly goal reached",
                    $"{updated.Minutes} conditioning minutes this week. +{GameMath.Quests.ConditioningWeeklyXp} XP", "Nice");
        }

        private async Task StartAsync(bool fromProgram)
        {
            // Location decides equipment, and so which swaps happen. Last-used location first.
            var last = UserSettings.LastLocation;
            var first = last == TrainingLocation.Gym ? "Gym" : "Home";
            var second = last == TrainingLocation.Gym ? "Home" : "Gym";
            var pick = await Shell.Current.DisplayActionSheet("Where are you training?", "Cancel", null, first, second);
            if (pick is not ("Gym" or "Home")) return;
            var location = pick == "Gym" ? TrainingLocation.Gym : TrainingLocation.Home;

            if (location == TrainingLocation.Home && await _training.GetEquipmentAsync(location) == Forge.Models.Equipment.None)
            {
                var setUp = await Shell.Current.DisplayAlert("No home gear set up",
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
