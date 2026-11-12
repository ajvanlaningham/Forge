using System.Globalization;
using System.Windows.Input;

using Forge.Constants;
using Forge.Resources.Strings;
using Forge.Services;
using Forge.Services.Interfaces;
using Forge.ViewModels.Controls.Cards;

namespace Forge.ViewModels
{
    /// <summary>
    /// Backs <see cref="Views.CheckInPage"/>. For now it owns the weight-entry form only;
    /// sleep, steps and protein hooks land here as later PBIs.
    /// </summary>
    public sealed class CheckInViewModel : BaseViewModel
    {
        private readonly IWeightService _weights;
        private readonly IDailyCheckInService _daily;
        private readonly IHealthData _health;

        private WeightUnit _displayUnit;
        private readonly AsyncRelayCommand _saveCommand;

        public CheckInViewModel(IWeightService weights, WeightTrendCardViewModel weightTrendCard,
            IDailyCheckInService daily, IHealthData health)
        {
            _weights = weights;
            _daily = daily;
            _health = health;
            SaveRecoveryCommand = new AsyncRelayCommand(SaveRecoveryAsync, () => true);
            HealthCommand = new AsyncRelayCommand(HealthAsync, () => true);
            WeightTrendCard = weightTrendCard;

            Title = AppResources.CheckInPage_Title;

            _displayUnit = UserSettings.WeightUnit;
            _entryDate = DateTime.Today;

            _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        }

        public WeightTrendCardViewModel WeightTrendCard { get; }

        public string WeightHint =>
            string.Format(AppResources.CheckInPage_WeightHint_Format, WeightMath.UnitLabel(_displayUnit));

        private string _weightText = string.Empty;
        public string WeightText
        {
            get => _weightText;
            set
            {
                if (SetProperty(ref _weightText, value))
                {
                    ClearStatus();
                    _saveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime _entryDate;
        /// <summary>
        /// Backfill is intentional — the user can log yesterday's number the next morning.
        /// The date picker's <c>MaximumDate</c> in XAML clamps to today, so the "future date"
        /// case never reaches the ViewModel.
        /// </summary>
        public DateTime EntryDate
        {
            get => _entryDate;
            set
            {
                if (!SetProperty(ref _entryDate, value)) return;
                ClearStatus();
                _ = LoadRecoveryAsync(sync: true);
            }
        }

        public DateTime MaximumEntryDate => DateTime.Today;

        private string _note = string.Empty;
        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                    HasStatus = !string.IsNullOrWhiteSpace(value);
            }
        }

        private bool _hasStatus;
        public bool HasStatus { get => _hasStatus; private set => SetProperty(ref _hasStatus, value); }

        public string PlaceholderText => AppResources.CheckInPage_Placeholder;

        public ICommand SaveCommand => _saveCommand;

        /// <summary>
        /// Called from <c>OnAppearing</c>: the unit toggle may have flipped in Settings while
        /// this page was elsewhere. If the user already typed a value, reconvert it into the
        /// new unit so the number on screen keeps meaning what the label says — otherwise a
        /// pounds entry would silently be saved as kilograms after another conversion. If the
        /// field is blank, refill from the latest logged entry in the new unit.
        /// </summary>
        public async Task RefreshAsync()
        {
            await _weights.InitAsync();

            var previousUnit = _displayUnit;
            var newUnit = UserSettings.WeightUnit;

            if (newUnit != previousUnit
                && !string.IsNullOrWhiteSpace(_weightText)
                && TryParseWeight(out var currentDisplay))
            {
                var pounds = WeightMath.ToStoredPounds(currentDisplay, previousUnit);
                var converted = WeightMath.ToDisplay(pounds, newUnit);
                _weightText = converted.ToString("0.0", CultureInfo.CurrentCulture);
                OnPropertyChanged(nameof(WeightText));
            }

            _displayUnit = newUnit;
            OnPropertyChanged(nameof(WeightHint));

            if (string.IsNullOrWhiteSpace(_weightText))
            {
                var latest = await _weights.GetLatestAsync();
                if (latest is not null)
                {
                    var displayValue = WeightMath.ToDisplay((double)latest.Pounds, _displayUnit);
                    WeightText = displayValue.ToString("0.0", CultureInfo.CurrentCulture);
                }
            }

            _saveCommand.RaiseCanExecuteChanged();

            // Refresh the trend card so the average and sparkline reflect any log made
            // elsewhere while this page was off-screen.
            await WeightTrendCard.RefreshAsync();

            await LoadRecoveryAsync(sync: true);
        }

        // ------------------------------------------------------------ recovery (PBI 3.4)

        private string _restingHrText = "", _sleepText = "", _stepsText = "";
        public string RestingHrText { get => _restingHrText; set => SetProperty(ref _restingHrText, value); }
        public string SleepText { get => _sleepText; set => SetProperty(ref _sleepText, value); }
        public string StepsText { get => _stepsText; set => SetProperty(ref _stepsText, value); }

        private string _restingHrSourceText = "", _sleepSourceText = "", _stepsSourceText = "";
        public string RestingHrSourceText { get => _restingHrSourceText; private set => SetProperty(ref _restingHrSourceText, value); }
        public string SleepSourceText { get => _sleepSourceText; private set => SetProperty(ref _sleepSourceText, value); }
        public string StepsSourceText { get => _stepsSourceText; private set => SetProperty(ref _stepsSourceText, value); }

        public string RecoveryDateText => DateOnly.FromDateTime(EntryDate) == DateOnly.FromDateTime(DateTime.Today)
            ? "For today. Sleep is last night's."
            : $"For {EntryDate:ddd MMM d}. Sleep is the night before.";

        private string _recoveryStatus = "";
        public string RecoveryStatus
        {
            get => _recoveryStatus;
            private set { if (SetProperty(ref _recoveryStatus, value)) OnPropertyChanged(nameof(HasRecoveryStatus)); }
        }
        public bool HasRecoveryStatus => RecoveryStatus.Length > 0;

        private string _healthText = "";
        public string HealthText { get => _healthText; private set => SetProperty(ref _healthText, value); }

        private string _healthButtonText = "";
        public string HealthButtonText { get => _healthButtonText; private set => SetProperty(ref _healthButtonText, value); }

        private bool _hasHealthButton;
        public bool HasHealthButton { get => _hasHealthButton; private set => SetProperty(ref _hasHealthButton, value); }

        public ICommand SaveRecoveryCommand { get; }
        public ICommand HealthCommand { get; }

        private bool _healthConnected;

        private async Task LoadRecoveryAsync(bool sync)
        {
            var date = DateOnly.FromDateTime(EntryDate);
            OnPropertyChanged(nameof(RecoveryDateText));
            RecoveryStatus = "";

            await RefreshHealthStatusAsync();
            if (sync && _healthConnected)
            {
                // Today and yesterday every time (late-arriving watch syncs), plus the day shown.
                var today = DateOnly.FromDateTime(DateTime.Today);
                await _daily.SyncAsync(today, today.AddDays(-1), date);
            }

            var row = await _daily.GetAsync(date);
            RestingHrText = row.RestingHr?.ToString(CultureInfo.CurrentCulture) ?? "";
            SleepText = row.SleepMinutes is int m ? HealthMath.FormatSleep(m) : "";
            StepsText = row.Steps?.ToString("N0", CultureInfo.CurrentCulture) ?? "";
            RestingHrSourceText = SourceText((DataSource)row.RestingHrSource);
            SleepSourceText = SourceText((DataSource)row.SleepSource);
            StepsSourceText = SourceText((DataSource)row.StepsSource);
        }

        private string SourceText(DataSource source) => source switch
        {
            DataSource.Synced => $"from {_health.StoreName}",
            DataSource.Manual => "typed in",
            _ => "",
        };

        private async Task RefreshHealthStatusAsync()
        {
            _healthConnected = false;
            switch (_health.Availability)
            {
                case HealthAvailability.Unsupported:
                    HealthText = "Type these in. Watch data isn't available on this phone.";
                    HasHealthButton = false;
                    return;
                case HealthAvailability.NeedsInstall:
                    HealthText = $"{_health.StoreName} needs installing or updating before Forge can read your watch or phone data.";
                    HealthButtonText = $"Get {_health.StoreName}";
                    HasHealthButton = true;
                    return;
            }
            _healthConnected = await _health.HasAnyPermissionAsync();
            HealthText = _healthConnected
                ? $"Filled from {_health.StoreName} when it has data. What you type always wins."
                : $"Fill these from your watch or phone through {_health.StoreName}, or just type them.";
            HealthButtonText = _healthConnected ? $"{_health.StoreName} settings" : $"Connect {_health.StoreName}";
            HasHealthButton = true;
        }

        private async Task HealthAsync()
        {
            if (_health.Availability != HealthAvailability.Available || _healthConnected)
            {
                _health.OpenSettings();
                return;
            }
            var granted = await _health.RequestPermissionsAsync();
            if (!granted)
            {
                RecoveryStatus = $"Nothing was shared. You can still type these, or connect later.";
                await RefreshHealthStatusAsync();
                return;
            }
            await LoadRecoveryAsync(sync: true);
            RecoveryStatus = $"Connected to {_health.StoreName}.";
        }

        private async Task SaveRecoveryAsync()
        {
            int? hr = null, sleep = null, steps = null;
            var hrText = RestingHrText.Trim();
            if (hrText.Length > 0)
            {
                if (!int.TryParse(hrText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var v) || !HealthMath.PlausibleRestingHr(v))
                { RecoveryStatus = "Resting heart rate should be a number of beats per minute, like 58."; return; }
                hr = v;
            }
            if (SleepText.Trim().Length > 0)
            {
                sleep = HealthMath.ParseSleep(SleepText);
                if (sleep is null) { RecoveryStatus = "Sleep reads like 7:30 or 7.5 (hours)."; return; }
            }
            var stepsText = StepsText.Trim();
            if (stepsText.Length > 0)
            {
                if (!int.TryParse(stepsText, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var v) || v < 0 || v > 200_000)
                { RecoveryStatus = "Steps should be a whole number."; return; }
                steps = v;
            }

            await _daily.SaveManualAsync(DateOnly.FromDateTime(EntryDate), hr, sleep, steps);
            await LoadRecoveryAsync(sync: false);
            RecoveryStatus = "Saved.";
        }

        private bool CanSave() => TryParseWeight(out _);

        private bool TryParseWeight(out double parsed)
        {
            if (double.TryParse(WeightText, NumberStyles.Float,
                CultureInfo.CurrentCulture, out parsed) && parsed > 0)
                return true;

            // Fall back to invariant so "185.5" typed with a period parses on locales that use commas.
            return double.TryParse(WeightText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed) && parsed > 0;
        }

        private void ClearStatus() => StatusMessage = string.Empty;

        private async Task SaveAsync()
        {
            if (!TryParseWeight(out var displayValue))
            {
                StatusMessage = AppResources.CheckInPage_InvalidWeight;
                return;
            }

            var pounds = (decimal)WeightMath.ToStoredPounds(displayValue, _displayUnit);
            var date = DateOnly.FromDateTime(EntryDate);

            var result = await _weights.LogAsync(date, pounds, Note);

            var display = WeightMath.ToDisplay((double)result.Entry.Pounds, _displayUnit);
            var unitLabel = WeightMath.UnitLabel(_displayUnit);

            // Correcting a day already logged earns nothing more (one award per day).
            StatusMessage = result.XpAwarded > 0
                ? string.Format(AppResources.CheckInPage_Saved_WithXp_Format,
                    display.ToString("0.0", CultureInfo.CurrentCulture), unitLabel, result.XpAwarded)
                : $"Updated: {display.ToString("0.0", CultureInfo.CurrentCulture)} {unitLabel}.";

            // Reflect the just-saved entry in the on-page trend card without waiting for
            // the next OnAppearing.
            await WeightTrendCard.RefreshAsync();
        }
    }
}
