using System.Collections.ObjectModel;
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
    public sealed class SessionHistoryItem
    {
        public required int SessionId { get; init; }
        public required string Title { get; init; }
        public required string DateText { get; init; }
        public required string Detail { get; init; }
    }

    /// <summary>Finished sessions, newest first (PBI 8.6).</summary>
    public sealed class SessionHistoryViewModel : BaseViewModel
    {
        private readonly ITrainingService _training;

        public ObservableCollection<SessionHistoryItem> Sessions { get; } = new();

        private bool _isEmpty;
        public bool IsEmpty { get => _isEmpty; private set => SetProperty(ref _isEmpty, value); }

        public ICommand OpenCommand { get; }

        public SessionHistoryViewModel(ITrainingService training)
        {
            _training = training;
            OpenCommand = new Command<int>(async id =>
                await Shell.Current.GoToAsync($"{nameof(SessionSummaryPage)}?sessionId={id}"));
        }

        public async Task LoadAsync()
        {
            var unit = UserSettings.WeightUnit;
            var u = WeightMath.UnitLabel(unit);
            var sessions = await _training.GetHistoryAsync();

            Sessions.Clear();
            foreach (var s in sessions)
            {
                var snap = await _training.GetSessionAsync(s.Id);
                var working = snap.Exercises.SelectMany(e => e.Sets).Where(x => !x.IsWarmup).ToList();
                var volume = working.Sum(x => WeightMath.ToDisplay(x.LoadLb, unit) * (x.Reps ?? 0));
                var minutes = s.EndedAtUtc is long end ? (int)Math.Round(TimeSpan.FromTicks(end - s.StartedAtUtc).TotalMinutes) : 0;
                var date = DateOnly.ParseExact(s.DateKey, "yyyy-MM-dd");

                Sessions.Add(new SessionHistoryItem
                {
                    SessionId = s.Id,
                    Title = s.Title ?? "Session",
                    DateText = date.ToString("ddd MMM d", CultureInfo.CurrentCulture),
                    Detail = $"{minutes} min · {working.Count} sets · {volume.ToString("N0", CultureInfo.CurrentCulture)} {u}"
                });
            }
            IsEmpty = Sessions.Count == 0;
        }
    }

    public sealed class ExerciseHistoryRow
    {
        public required string DateText { get; init; }
        public required string SetsText { get; init; }
        public required string BestText { get; init; }
    }

    /// <summary>One exercise over time: best e1RM chart and every working set (PBI 8.6).</summary>
    public sealed class ExerciseHistoryViewModel : BaseViewModel
    {
        private static readonly Color Accent = Color.FromArgb("#D4AF37");
        private static readonly Color Label = Color.FromArgb("#9AA4B2");

        private readonly ITrainingService _training;

        public ObservableCollection<ExerciseHistoryRow> Rows { get; } = new();

        private string _name = "";
        public string Name { get => _name; private set => SetProperty(ref _name, value); }

        private string _headline = "";
        public string Headline { get => _headline; private set => SetProperty(ref _headline, value); }

        private string _chartCaption = "";
        public string ChartCaption { get => _chartCaption; private set => SetProperty(ref _chartCaption, value); }

        private IDrawable? _chart;
        public IDrawable? Chart { get => _chart; private set => SetProperty(ref _chart, value); }

        private bool _hasChart;
        public bool HasChart { get => _hasChart; private set => SetProperty(ref _hasChart, value); }

        private bool _isEmpty;
        public bool IsEmpty { get => _isEmpty; private set => SetProperty(ref _isEmpty, value); }

        public ExerciseHistoryViewModel(ITrainingService training) => _training = training;

        public async Task LoadAsync(string exerciseName)
        {
            Name = exerciseName;
            var unit = UserSettings.WeightUnit;
            var u = WeightMath.UnitLabel(unit);
            var history = await _training.GetExerciseHistoryAsync(exerciseName);
            IsEmpty = history.Count == 0;

            string Fmt(double lb) => LoadDisplay.Short(lb);   // "25", or "16 kg" for a kilo bell

            // Loaded lifts chart best e1RM; bodyweight and timed work chart best reps/seconds.
            var loaded = history.SelectMany(h => h.WorkingSets).Any(s => s.LoadLb > 0 && s.Reps is not null);
            var timed = !loaded && history.SelectMany(h => h.WorkingSets).Any(s => s.Seconds is not null);

            double Best(IReadOnlyList<SessionSetRow> sets) => loaded
                ? WeightMath.ToDisplay(StrengthMath.BestE1rm(sets.Select(s => new WorkSet(s.LoadLb, s.Reps, s.Seconds))), unit)
                : timed ? sets.Max(s => s.Seconds ?? 0) : sets.Max(s => s.Reps ?? 0);

            ChartCaption = loaded ? $"Best estimated 1RM ({u})" : timed ? "Longest hold (s)" : "Most reps";

            var points = history.Select(h => (h.Date, Value: Best(h.WorkingSets))).ToList();
            HasChart = points.Count >= 2;
            // Fit the axis to the data: from zero, a 5% strength gain is an invisible wiggle.
            var lo = points.Count > 0 ? points.Min(p => p.Value) : 0;
            var hi = points.Count > 0 ? points.Max(p => p.Value) : 1;
            var pad = Math.Max(1, (hi - lo) * 0.25);
            Chart = new Forge.ViewModels.Controls.LineChart(
                points.Select(p => (p.Date.ToString("M/d", CultureInfo.CurrentCulture), p.Value,
                    p.Value.ToString("0", CultureInfo.CurrentCulture))).ToList(),
                Math.Max(0, lo - pad), hi + pad, Accent, Label);

            if (points.Count > 0)
            {
                var first = points[0].Value;
                var latest = points[^1].Value;
                var unitText = loaded ? $" {u}" : timed ? " s" : " reps";
                Headline = points.Count == 1
                    ? $"Baseline: {latest:0}{unitText}"
                    : $"{first:0} → {latest:0}{unitText} over {points.Count} sessions";
            }

            Rows.Clear();
            foreach (var h in Enumerable.Reverse(history))
            {
                Rows.Add(new ExerciseHistoryRow
                {
                    DateText = h.Date.ToString("ddd MMM d", CultureInfo.CurrentCulture),
                    SetsText = string.Join(", ", h.WorkingSets.Select(s =>
                        (s.LoadLb > 0 ? $"{Fmt(s.LoadLb)}×" : "") + (s.Seconds is int sec ? $"{sec}s" : $"{s.Reps}"))),
                    BestText = loaded ? $"e1RM {Best(h.WorkingSets):0} {u}" : ""
                });
            }
        }
    }
}
