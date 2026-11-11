using Forge.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Forge.Services.Interfaces;
using Forge.Training;
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

    /// <summary>
    /// The quest underway, at the top of the Quest tab (Decision 20): where you are in the block,
    /// this week's days and how close the item is to being forged.
    /// </summary>
    public sealed class CurrentQuestViewModel : BaseViewModel
    {
        private readonly IProgramService _programs;
        private readonly IForgeService _forge;

        public ObservableCollection<ProgramDayItem> Days { get; } = new();

        private string _programName = "";
        public string ProgramName { get => _programName; private set => SetProperty(ref _programName, value); }

        private string? _programDescription;
        public string? ProgramDescription { get => _programDescription; private set => SetProperty(ref _programDescription, value); }

        private bool _hasProgram;
        public bool HasProgram { get => _hasProgram; private set => SetProperty(ref _hasProgram, value); }

        private string _weekText = "";
        public string WeekText { get => _weekText; private set => SetProperty(ref _weekText, value); }

        private string _blockText = "";
        public string BlockText { get => _blockText; private set => SetProperty(ref _blockText, value); }

        private string _forgeText = "";
        /// <summary>"Forge meter: 3 of 16 training days · 12 needed (75%)".</summary>
        public string ForgeText { get => _forgeText; private set => SetProperty(ref _forgeText, value); }

        private double _forgeProgress;
        /// <summary>Progress toward the 75% needed to forge (1.0 = enough).</summary>
        public double ForgeProgress { get => _forgeProgress; private set => SetProperty(ref _forgeProgress, value); }

        private string? _weekNote;
        public string? WeekNote { get => _weekNote; private set { if (SetProperty(ref _weekNote, value)) OnPropertyChanged(nameof(HasWeekNote)); } }
        public bool HasWeekNote => WeekNote is not null;

        private bool _isBlockComplete;
        public bool IsBlockComplete { get => _isBlockComplete; private set => SetProperty(ref _isBlockComplete, value); }

        public ICommand RepeatBlockCommand { get; }
        public ICommand OpenLibraryCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(ExerciseLibraryPage)));

        public CurrentQuestViewModel(IProgramService programs, IForgeService forge)
        {
            _programs = programs;
            _forge = forge;
            RepeatBlockCommand = new Command(async () => await RepeatBlockAsync());
        }

        public async Task RefreshAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var view = await _programs.GetViewAsync(today);

            Days.Clear();
            HasProgram = view is not null;
            if (view is null)
            {
                ProgramName = "No quest underway";
                ProgramDescription = "Accept a quest from the board below.";
                WeekText = BlockText = ForgeText = "";
                ForgeProgress = 0;
                WeekNote = null;
                IsBlockComplete = false;
                return;
            }

            var def = view.Program.Definition;
            ProgramName = $"{HeroQuest.Name(def)} · {def.Discipline}";
            ProgramDescription = def.Description;
            IsBlockComplete = view.BlockComplete;
            var (wStart, wEnd) = BlockSchedule.WeekRange(view.Program.StartedOn, view.Week);
            var result = view.BlockComplete ? await _forge.GetResultAsync(view.Program.BlockId) : null;
            WeekText = view.BlockComplete
                ? result is null ? $"The {HeroQuest.Name(def)} is ready to forge"
                  : result.Forged ? $"Forged: the {HeroQuest.Name(def)}"
                  : "The metal cooled. Try again or choose the next quest."
                : $"Week {view.Week} of {ProgramDefinition.WeeksPerBlock} · {ProgramDefinition.WeekName(view.Week)}";
            BlockText = $"{def.VersionName} · this week {wStart:MMM d}–{wEnd:MMM d}";
            WeekNote = view.Week == ProgramDefinition.LightWeek && !view.BlockComplete
                ? "Honing week: same weights, one set fewer. The stat tests happen this week."
                : null;

            // Forging (Decision 18): 75% of the block's training days, judged at the end.
            var total = Forging.TotalDays(def.TrainingDaysPerWeek);
            var needed = Forging.Needed(def.TrainingDaysPerWeek);
            var done = await _programs.CompletedDaysInBlockAsync(view.Program.BlockId);
            ForgeProgress = Math.Min(1.0, (double)done / needed);
            ForgeText = done >= needed
                ? $"Forge meter: {done} of {total} days · enough to forge the {HeroQuest.Name(def)} at the block's end"
                : $"Forge meter: {done} of {total} days · {needed} needed to forge the {HeroQuest.Name(def)}";

            for (var i = 0; i < def.Days.Count; i++)
            {
                var day = def.Resolve(i, view.Week);
                var isDone = view.DoneThisWeek.Contains(i);
                Days.Add(new ProgramDayItem
                {
                    Number = i + 1,
                    Name = day.Name,
                    Summary = string.Join("\n", day.Slots.Select(r => TodayViewModel.Describe(r.Slot) + (r.IsMain ? "" : "  · acc"))),
                    IsNext = view.Day?.DayIndex == i && !view.CompletedOnDate,
                    IsDone = isDone,
                    IsDoneToday = isDone && view.Day?.DayIndex == i && view.CompletedOnDate
                });
            }
        }

        private async Task RepeatBlockAsync()
        {
            var active = await _programs.GetActiveAsync();
            if (active is null) return;
            await _programs.StartProgramAsync(active.Definition.Id, DateOnly.FromDateTime(DateTime.Today));
            await RefreshAsync();
        }
    }
}
