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

        // The one celebration after a session (Decision 26): three tiles, the week strip, a level banner.
        private string _minutesTile = "", _setsTile = "", _xpTile = "", _xpCaption = "XP", _weekStrip = "", _weekText = "", _levelBanner = "";
        public string MinutesTile { get => _minutesTile; private set => SetProperty(ref _minutesTile, value); }
        public string SetsTile { get => _setsTile; private set => SetProperty(ref _setsTile, value); }
        public string XpTile { get => _xpTile; private set => SetProperty(ref _xpTile, value); }
        public string XpCaption { get => _xpCaption; private set => SetProperty(ref _xpCaption, value); }
        /// <summary>This week's training days as circles: ● done, ○ to do.</summary>
        public string WeekStrip { get => _weekStrip; private set => SetProperty(ref _weekStrip, value); }
        public string WeekText { get => _weekText; private set => SetProperty(ref _weekText, value); }
        public string LevelBanner { get => _levelBanner; set { if (SetProperty(ref _levelBanner, value)) OnPropertyChanged(nameof(HasLevelBanner)); } }
        public bool HasLevelBanner => !string.IsNullOrEmpty(LevelBanner);
        private bool _showDetails;
        public bool ShowDetails { get => _showDetails; set { if (SetProperty(ref _showDetails, value)) OnPropertyChanged(nameof(DetailsText)); } }
        public string DetailsText => ShowDetails ? "HIDE DETAILS" : "SEE EVERY SET";
        public ICommand ToggleDetailsCommand => new Command(() => ShowDetails = !ShowDetails);

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
            XpText = day is { } d
                ? $"Training day {d.DayIndex + 1} complete · +{d.Xp} XP"
                : null;
            OnPropertyChanged(nameof(HasXp));

            MinutesTile = $"{minutes}";
            SetsTile = $"{working.Count}";
            XpTile = day is { } dx ? $"+{dx.Xp}" : "—";
            var today = DateOnly.FromDateTime(DateTime.Today);
            var multiplier = await _programs.MultiplierForAsync(today);
            XpCaption = multiplier > 1.0 ? $"XP ×{multiplier:0.0}" : "XP";
            if (await _programs.GetViewAsync(today) is { } view)
            {
                WeekStrip = string.Join("  ", Enumerable.Range(0, view.DayCount).Select(i => view.DoneThisWeek.Contains(i) ? "●" : "○"));
                WeekText = $"{view.DoneThisWeek.Count} of {view.DayCount} training days this week";
            }

            var records = await _training.GetSessionRecordsAsync(sessionId);

            Lines.Clear();
            foreach (var ex in snap.Exercises)
            {
                var sets = ex.Sets.Where(s => !s.IsWarmup).ToList();
                var detail = sets.Count == 0
                    ? (ex.Row.Skipped ? "skipped" : "no working sets")
                    : string.Join(", ", sets.Select(s =>
                        (s.LoadLb > 0 ? LoadDisplay.Short(s.LoadLb) + "×" : "")
                        + (s.Seconds is int sec ? $"{sec}s" : $"{s.Reps}")));
                // Next time: progression from this session (now finished, so it is the most recent).
                string? next = null;
                if (sets.Count > 0)
                {
                    var history = await _training.GetRecentWorkingSetsAsync(ex.Row.ExerciseName);
                    var loads = UserSettings.GearAt(snap.Location).For(ex.Info?.Modality ?? Forge.Models.Modality.Dumbbell, unit, ex.Info?.NeedsPair ?? false);
                    if (Progression.Suggest(ExerciseCardViewModel.RxOf(ex.Row), history, loads, unit) is { } sug)
                    {
                        var load = sug.LoadLb > 0 ? LoadDisplay.Label(sug.LoadLb) + " × " : "";
                        next = $"Next time: {load}{(sug.Seconds is int sec ? $"{sec}s" : $"{sug.Reps}")}";
                    }
                }
                string? recordText = null;
                if (records.TryGetValue(ex.Row.ExerciseName, out var prs))
                    recordText = "Record: " + string.Join(" · ", prs.Select(pr => pr.Kind switch
                    {
                        PrKind.HeaviestLoad => $"heaviest {LoadDisplay.Label(pr.LoadLb)}",
                        PrKind.BestE1rm => $"best e1RM {WeightMath.ToDisplay(pr.Value, unit):0} {u}",
                        _ => pr.LoadLb > 0 ? $"{pr.Reps} reps at {LoadDisplay.Label(pr.LoadLb)}" : $"{pr.Reps} reps"
                    }));
                Lines.Add(new SummaryLine { Name = ex.Row.ExerciseName, Detail = detail, Next = next, Records = recordText });
            }
        }
    }
}
