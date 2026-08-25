using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

using Forge.Common;
using Forge.Constants;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services;
using Forge.Services.Interfaces;

using Microcharts;

using SkColor = SkiaSharp.SKColor;

namespace Forge.ViewModels.Controls.Cards
{
    /// <summary>
    /// Backs <see cref="Forge.Controls.Cards.WeightTrendCard"/>. Reads the last
    /// <see cref="WeightMath.MovingAverageWindowDays"/> of logs from
    /// <see cref="IWeightService"/> and exposes the N-day average, today's raw value if any,
    /// and a Microcharts <see cref="ChartEntry"/> list for the sparkline.
    /// </summary>
    /// <remarks>
    /// The card is used on Home and on Check-in. Each host owns its own instance (transient
    /// in DI) so tap-to-expand state does not leak across pages.
    /// </remarks>
    public sealed class WeightTrendCardViewModel : BaseViewModel
    {
        // Accent (#D4AF37) — the theme's gold trim. Kept in sync with Resources/Styles/Colors.xaml
        // because Microcharts paints on the Skia surface, not through MAUI resources.
        private static readonly SkColor AccentSk = new(0xD4, 0xAF, 0x37);
        private static readonly SkColor SurfaceSk = new(0x16, 0x1B, 0x22);

        private readonly IWeightService _weights;

        public WeightTrendCardViewModel(IWeightService weights)
        {
            _weights = weights;
            Title = AppResources.WeightTrendCard_Title;

            ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
            GoToCheckInCommand = new AsyncRelayCommand(async () =>
                await Shell.Current.GoToAsync("//checkin"));
        }

        private string _averageDisplay = AppResources.WeightTrendCard_Empty;
        /// <summary>Big number shown in the card, e.g. "185.5 lb" or "--" if no data.</summary>
        public string AverageDisplay
        {
            get => _averageDisplay;
            private set => SetProperty(ref _averageDisplay, value);
        }

        private string _averageLabelText = string.Empty;
        /// <summary>
        /// Small label under the number, e.g. "7-day avg" or "1-day avg". Reflects the number
        /// of days that actually contributed to the average, not the window size.
        /// </summary>
        public string AverageLabelText
        {
            get => _averageLabelText;
            private set => SetProperty(ref _averageLabelText, value);
        }

        private string _rawDisplay = string.Empty;
        /// <summary>
        /// The expanded detail line: either "Today: 184.2 lb" if today has a log, or the
        /// "not logged" message. Bound in XAML behind <see cref="IsExpanded"/>.
        /// </summary>
        public string RawDisplay
        {
            get => _rawDisplay;
            private set => SetProperty(ref _rawDisplay, value);
        }

        private bool _isExpanded;
        /// <summary>Toggled by tapping the card. Controls the raw detail section visibility.</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _hasData;
        /// <summary>False when there are no entries in the window at all. Card shows "--".</summary>
        public bool HasData
        {
            get => _hasData;
            private set
            {
                if (SetProperty(ref _hasData, value))
                    OnPropertyChanged(nameof(HasNoData));
            }
        }

        /// <summary>Convenience inverse for the empty-state hint.</summary>
        public bool HasNoData => !_hasData;

        private bool _hasLoggedToday;
        /// <summary>True when today has an entry. Drives the "log today" CTA in the expand pane.</summary>
        public bool HasLoggedToday
        {
            get => _hasLoggedToday;
            private set
            {
                if (SetProperty(ref _hasLoggedToday, value))
                    OnPropertyChanged(nameof(NeedsLogToday));
            }
        }

        /// <summary>Inverse for XAML visibility on the "not logged" CTA.</summary>
        public bool NeedsLogToday => !_hasLoggedToday;

        private IEnumerable<ChartEntry> _sparklineEntries = System.Array.Empty<ChartEntry>();
        /// <summary>
        /// Points fed to Microcharts' LineChart. Missing days are dropped, never zero-filled —
        /// a flat line at 0 for the days a user skipped would look like they logged 0 lb.
        /// </summary>
        public IEnumerable<ChartEntry> SparklineEntries
        {
            get => _sparklineEntries;
            private set => SetProperty(ref _sparklineEntries, value);
        }

        private LineChart _sparklineChart = BuildEmptyChart();
        /// <summary>
        /// Ready-to-render chart bound to the ChartView. Re-built on every refresh so the
        /// visible line reflects the current entries (Microcharts doesn't observe changes to
        /// the Entries collection after the chart is bound).
        /// </summary>
        public LineChart SparklineChart
        {
            get => _sparklineChart;
            private set => SetProperty(ref _sparklineChart, value);
        }

        public ICommand ToggleExpandedCommand { get; }
        public ICommand GoToCheckInCommand { get; }

        /// <summary>
        /// Pull the last 7 days, recompute the average + sparkline. Call from the host page's
        /// OnAppearing so the card reflects a save that just happened on Check-in.
        /// </summary>
        public async Task RefreshAsync()
        {
            await _weights.InitAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var unit = UserSettings.WeightUnit;
            var unitLabel = WeightMath.UnitLabel(unit);

            var recent = await _weights.GetRecentAsync(today, WeightMath.MovingAverageWindowDays);

            var sample = WeightMath.MovingAverageWithSample(
                recent.Select(e => (e.Date, WeightPounds: (double)e.Pounds)),
                today);

            var todayEntry = recent.FirstOrDefault(e => e.Date == today);
            HasLoggedToday = todayEntry is not null;

            if (sample is null)
            {
                HasData = false;
                AverageDisplay = AppResources.WeightTrendCard_Empty;
                AverageLabelText = string.Empty;
                RawDisplay = AppResources.WeightTrendCard_NotLoggedToday;
                SparklineEntries = System.Array.Empty<ChartEntry>();
                SparklineChart = BuildEmptyChart();
                return;
            }

            var s = sample.Value;
            var avgDisplay = WeightMath.ToDisplay(s.Average, unit);
            AverageDisplay = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.Home_Weight_Value_Format,
                avgDisplay,
                unitLabel);
            AverageLabelText = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.WeightTrendCard_AverageLabel_Format,
                s.SampleDays);
            HasData = true;

            RawDisplay = todayEntry is not null
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.WeightTrendCard_Today_Format,
                    WeightMath.ToDisplay((double)todayEntry.Pounds, unit),
                    unitLabel)
                : AppResources.WeightTrendCard_NotLoggedToday;

            var entries = recent
                .OrderBy(e => e.Date)
                .Select(e => new ChartEntry((float)WeightMath.ToDisplay((double)e.Pounds, unit))
                {
                    Color = AccentSk,
                })
                .ToList();

            SparklineEntries = entries;
            SparklineChart = BuildChart(entries);
        }

        private static LineChart BuildEmptyChart() => BuildChart(System.Array.Empty<ChartEntry>());

        private static LineChart BuildChart(IEnumerable<ChartEntry> entries) => new()
        {
            Entries = entries,
            BackgroundColor = SurfaceSk,
            LineMode = LineMode.Straight,
            LineSize = 4,
            PointMode = PointMode.Circle,
            PointSize = 8,
            LabelTextSize = 0,
            Margin = 8,
        };
    }

    /// <summary>Minimal synchronous ICommand for parameter-less UI hooks.</summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly System.Action _execute;
        public RelayCommand(System.Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event System.EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
