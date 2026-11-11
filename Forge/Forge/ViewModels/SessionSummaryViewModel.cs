using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Forge.Constants;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.Training;

namespace Forge.ViewModels
{
    public sealed class SummaryLine
    {
        public required string Name { get; init; }
        public required string Detail { get; init; }
        public string? Next { get; init; }
        public bool HasNext => Next is not null;
        /// <summary>Records set this session — stated plainly, no fanfare (they earn nothing).</summary>
        public string? Records { get; init; }
        public bool HasRecords => Records is not null;
    }

    /// <summary>End-of-session summary (PBI 8.1 basics; PRs and next-time suggestions arrive with 8.5/8.6).</summary>
    public sealed class SessionSummaryViewModel : BaseViewModel
    {
        private readonly ITrainingService _training;
        private readonly IProgramService _programs;

        private string? _xpText;
        public string? XpText { get => _xpText; private set => SetProperty(ref _xpText, value); }
        public bool HasXp => XpText is not null;

        public ICommand OpenExerciseCommand { get; } = new Command<string>(async name =>
        {
            if (!string.IsNullOrEmpty(name)) await Forge.Views.ExerciseHistoryPage.OpenAsync(name);
        });

        public ObservableCollection<SummaryLine> Lines { get; } = new();

        private string _heading = "";
        public string Heading { get => _heading; private set => SetProperty(ref _heading, value); }

        private string _stats = "";
        public string Stats { get => _stats; private set => SetProperty(ref _stats, value); }

        public SessionSummaryViewModel(ITrainingService training, IProgramService programs)
        {
            _training = training;
            _programs = programs;
        }

        public async Task LoadAsync(int sessionId)
        {
            var snap = await _training.GetSessionAsync(sessionId);
            var unit = UserSettings.WeightUnit;
            var u = WeightMath.UnitLabel(unit);

            var started = new DateTime(snap.Session.StartedAtUtc, DateTimeKind.Utc);
            var ended = snap.Session.EndedAtUtc is long e ? new DateTime(e, DateTimeKind.Utc) : DateTime.UtcNow;
            var minutes = (int)Math.Round((ended - started).TotalMinutes);

            var working = snap.Exercises.SelectMany(x => x.Sets).Where(s => !s.IsWarmup).ToList();
            var volume = working.Sum(s => WeightMath.ToDisplay(s.LoadLb, unit) * (s.Reps ?? 0));

            Heading = snap.Session.Title ?? "Session";
            Stats = $"{minutes} min · {working.Count} working sets · {volume.ToString("N0", CultureInfo.CurrentCulture)} {u} volume";

            var day = await _programs.DayCompletedBySessionAsync(sessionId);
            XpText = day is int d
                ? $"Day {d + 1} complete · +{Forge.Constants.GameMath.Quests.XpPerProgramDay} XP"
                : null;
            OnPropertyChanged(nameof(HasXp));

            var records = await _training.GetSessionRecordsAsync(sessionId);

            Lines.Clear();
            foreach (var ex in snap.Exercises)
            {
                var sets = ex.Sets.Where(s => !s.IsWarmup).ToList();
                var detail = sets.Count == 0
                    ? (ex.Row.Skipped ? "skipped" : "no working sets")
                    : string.Join(", ", sets.Select(s =>
                        (s.LoadLb > 0 ? WeightMath.ToDisplay(s.LoadLb, unit).ToString("0.#", CultureInfo.CurrentCulture) + "×" : "")
                        + (s.Seconds is int sec ? $"{sec}s" : $"{s.Reps}")));
                // Next time: progression from this session (now finished, so it is the most recent).
                string? next = null;
                if (sets.Count > 0)
                {
                    var history = await _training.GetRecentWorkingSetsAsync(ex.Row.ExerciseName);
                    var step = LoadIncrements.StepFor(ex.Info?.Modality ?? Forge.Models.Modality.Dumbbell, unit);
                    if (Progression.Suggest(ExerciseCardViewModel.RxOf(ex.Row), history, step, unit) is { } sug)
                    {
                        var load = sug.LoadLb > 0
                            ? WeightMath.ToDisplay(sug.LoadLb, unit).ToString("0.#", CultureInfo.CurrentCulture) + $" {u} × "
                            : "";
                        next = $"Next time: {load}{(sug.Seconds is int sec ? $"{sec}s" : $"{sug.Reps}")}";
                    }
                }
                string? recordText = null;
                if (records.TryGetValue(ex.Row.ExerciseName, out var prs))
                    recordText = "Record: " + string.Join(" · ", prs.Select(pr => pr.Kind switch
                    {
                        PrKind.HeaviestLoad => $"heaviest {WeightMath.ToDisplay(pr.LoadLb, unit):0.#} {u}",
                        PrKind.BestE1rm => $"best e1RM {WeightMath.ToDisplay(pr.Value, unit):0} {u}",
                        _ => pr.LoadLb > 0 ? $"{pr.Reps} reps at {WeightMath.ToDisplay(pr.LoadLb, unit):0.#} {u}" : $"{pr.Reps} reps"
                    }));
                Lines.Add(new SummaryLine { Name = ex.Row.ExerciseName, Detail = detail, Next = next, Records = recordText });
            }
        }
    }
}
