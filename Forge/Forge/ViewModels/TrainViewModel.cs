using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;
using Forge.Views;
using Forge.Views.SubPages;

namespace Forge.ViewModels
{
    /// <summary>One training day of the current week.</summary>
    public sealed class ProgramDayItem
    {
        public int Number { get; init; }
        public string Name { get; init; } = "";
        public string Summary { get; init; } = "";
        public bool IsNext { get; init; }
        public bool IsDone { get; init; }
        public bool IsDoneToday { get; init; }
        public string Header => $"{Number} · {Name}";
        public string StatusText => IsDoneToday ? "DONE TODAY" : IsDone ? "DONE" : IsNext ? "UP NEXT" : "";
        public bool HasStatus => IsDone || IsNext;
    }

    public sealed class TrainViewModel : BaseViewModel
    {
        private readonly IProgramService _programs;
        private readonly ITrainingService _training;
        private int? _inProgressId;

        public ObservableCollection<ProgramDayItem> Days { get; } = new();

        private string _programName = "";
        public string ProgramName { get => _programName; private set => SetProperty(ref _programName, value); }

        private string? _programDescription;
        public string? ProgramDescription { get => _programDescription; private set => SetProperty(ref _programDescription, value); }

        private bool _isPlaceholder;
        public bool IsPlaceholder { get => _isPlaceholder; private set => SetProperty(ref _isPlaceholder, value); }

        private bool _hasProgram;
        public bool HasProgram { get => _hasProgram; private set => SetProperty(ref _hasProgram, value); }

        private string _nextDayText = "";
        public string NextDayText { get => _nextDayText; private set => SetProperty(ref _nextDayText, value); }

        private string _blockText = "";
        public string BlockText { get => _blockText; private set => SetProperty(ref _blockText, value); }

        private string _weekText = "";
        /// <summary>"Week 2 of 4".</summary>
        public string WeekText { get => _weekText; private set => SetProperty(ref _weekText, value); }

        private string? _weekNote;
        /// <summary>Week 4: "Light week — same weights, fewer sets. Stat tests this week."</summary>
        public string? WeekNote { get => _weekNote; private set { if (SetProperty(ref _weekNote, value)) OnPropertyChanged(nameof(HasWeekNote)); } }
        public bool HasWeekNote => WeekNote is not null;

        private string _streakText = "";
        public string StreakText { get => _streakText; private set => SetProperty(ref _streakText, value); }

        private bool _isBlockComplete;
        public bool IsBlockComplete { get => _isBlockComplete; private set => SetProperty(ref _isBlockComplete, value); }

        private string _startText = "";
        public string StartText { get => _startText; private set => SetProperty(ref _startText, value); }

        private bool _canStartProgram;
        public bool CanStartProgram { get => _canStartProgram; private set => SetProperty(ref _canStartProgram, value); }

        private bool _hasInProgress;
        public bool HasInProgress { get => _hasInProgress; private set { if (SetProperty(ref _hasInProgress, value)) OnPropertyChanged(nameof(NoInProgress)); } }
        public bool NoInProgress => !HasInProgress;

        private string _resumeText = "";
        public string ResumeText { get => _resumeText; private set => SetProperty(ref _resumeText, value); }

        public ICommand StartProgramCommand { get; }
        public ICommand StartEmptyCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand ChangeProgramCommand { get; }
        public ICommand RepeatBlockCommand { get; }
        public ICommand OpenLibraryCommand { get; }
        public ICommand OpenGearCommand { get; }
        public ICommand OpenHistoryCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(SessionHistoryPage)));

        public TrainViewModel(IProgramService programs, ITrainingService training)
        {
            _programs = programs;
            _training = training;
            StartProgramCommand = new Command(async () => await StartAsync(fromProgram: true));
            StartEmptyCommand = new Command(async () => await StartAsync(fromProgram: false));
            ResumeCommand = new Command(async () => await OpenSessionAsync(_inProgressId));
            Title = "Train";
            ChangeProgramCommand = new Command(async () => await ChangeProgramAsync());
            RepeatBlockCommand = new Command(async () => await RepeatBlockAsync());
            OpenLibraryCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(ExerciseLibraryPage)));
            OpenGearCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(MyGearPage)));
        }

        public async Task RefreshAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var view = await _programs.GetViewAsync(today);

                var open = await _training.GetInProgressAsync();
                _inProgressId = open?.Session.Id;
                HasInProgress = open is not null;
                ResumeText = open is null ? "" : $"Resume: {open.Session.Title}";
                CanStartProgram = view is { Day: not null, CompletedOnDate: false, BlockComplete: false } && open is null;
                StartText = view?.Day is { } d ? $"Start {d.Name}" + (d.IsLight ? " (light)" : "") : "";

                Days.Clear();
                HasProgram = view is not null;
                if (view is null)
                {
                    ProgramName = "No program";
                    ProgramDescription = "Pick a program to get a planned session every training day.";
                    IsPlaceholder = false;
                    NextDayText = WeekText = BlockText = StreakText = "";
                    WeekNote = null;
                    IsBlockComplete = false;
                    return;
                }

                var def = view.Program.Definition;
                ProgramName = def.Name;
                ProgramDescription = def.Description;
                IsPlaceholder = def.Placeholder;
                IsBlockComplete = view.BlockComplete;
                var (wStart, wEnd) = BlockSchedule.WeekRange(view.Program.StartedOn, view.Week);
                WeekText = view.BlockComplete ? "Block complete" : $"Week {view.Week} of {ProgramDefinition.WeeksPerBlock}";
                BlockText = $"{def.TrainingDaysPerWeek} training days + active rest · this week {wStart:MMM d}–{wEnd:MMM d}";
                WeekNote = view.Week == ProgramDefinition.LightWeek && !view.BlockComplete
                    ? "Light week: same weights, one set fewer. The stat tests happen this week."
                    : null;
                StreakText = view.PerfectWeekStreak > 0
                    ? $"Perfect weeks in a row: {view.PerfectWeekStreak} · ×{view.Multiplier:0.0} XP this week"
                    : "Move every day and finish this week's training for a ×1.2 XP week next.";
                NextDayText = view.BlockComplete ? "Four weeks done. Repeat the block or pick the next one."
                    : view.Day is null ? "This week's training is done. Active rest the remaining days."
                    : view.CompletedOnDate ? $"{view.Day.Name} done today."
                    : $"Today: {view.Day.Name}";

                for (var i = 0; i < def.Days.Count; i++)
                {
                    var day = def.Resolve(i, view.Week);
                    var done = view.DoneThisWeek.Contains(i);
                    Days.Add(new ProgramDayItem
                    {
                        Number = i + 1,
                        Name = day.Name,
                        Summary = string.Join("\n", day.Slots.Select(r => Describe(r.Slot) + (r.IsMain ? "" : "  · acc"))),
                        IsNext = view.Day?.DayIndex == i && !view.CompletedOnDate,
                        IsDone = done,
                        IsDoneToday = done && view.Day?.DayIndex == i && view.CompletedOnDate
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        internal static string Describe(ProgramSlot s)
        {
            var per = s.IsTimed ? $"{s.Seconds}s"
                : s.RepsMin == s.RepsMax ? $"{s.RepsMin}" : $"{s.RepsMin}–{s.RepsMax}";
            return $"{s.Exercise}  {s.Sets} × {per}";
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

            var today = DateOnly.FromDateTime(DateTime.Today);
            var snap = fromProgram
                ? await _training.StartFromProgramAsync(today, location)
                : await _training.StartEmptyAsync(today, location);
            await OpenSessionAsync(snap.Session.Id);
        }

        private static async Task OpenSessionAsync(int? sessionId)
        {
            if (sessionId is null) return;
            await Shell.Current.GoToAsync($"{nameof(ActiveSessionPage)}?sessionId={sessionId}");
        }

        private async Task RepeatBlockAsync()
        {
            var active = await _programs.GetActiveAsync();
            if (active is null) return;
            await _programs.StartProgramAsync(active.Definition.Id, DateOnly.FromDateTime(DateTime.Today));
            await RefreshAsync();
        }

        private async Task ChangeProgramAsync()
        {
            var programs = await _programs.GetProgramsAsync();
            if (programs.Count == 0) return;

            var labels = programs.Select(p => p.Placeholder ? $"{p.Name} (placeholder)" : p.Name).ToArray();
            var choice = await Shell.Current.DisplayActionSheet("Choose a program", "Cancel", null, labels);
            var index = Array.IndexOf(labels, choice);
            if (index < 0) return;

            var picked = programs[index];
            var confirm = await Shell.Current.DisplayAlert(
                "Start program?",
                $"Start \"{picked.Name}\" at week 1, today? Your current block ends. Workout history is kept.",
                "Start", "Cancel");
            if (!confirm) return;

            await _programs.StartProgramAsync(picked.Id, DateOnly.FromDateTime(DateTime.Today));
            await RefreshAsync();
        }
    }
}
