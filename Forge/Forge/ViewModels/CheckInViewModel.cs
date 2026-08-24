using System.Globalization;
using System.Windows.Input;

using Forge.Constants;
using Forge.Resources.Strings;
using Forge.Services;
using Forge.Services.Interfaces;

namespace Forge.ViewModels
{
    /// <summary>
    /// Backs <see cref="Views.CheckInPage"/>. For now it owns the weight-entry form only;
    /// sleep, steps and protein hooks land here as later PBIs.
    /// </summary>
    public sealed class CheckInViewModel : BaseViewModel
    {
        private readonly IWeightService _weights;

        private WeightUnit _displayUnit;
        private readonly AsyncRelayCommand _saveCommand;

        public CheckInViewModel(IWeightService weights)
        {
            _weights = weights;

            Title = AppResources.CheckInPage_Title;

            _displayUnit = UserSettings.WeightUnit;
            _entryDate = DateTime.Today;

            _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        }

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
            set { if (SetProperty(ref _entryDate, value)) ClearStatus(); }
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

            StatusMessage = result.XpAwarded > 0
                ? string.Format(AppResources.CheckInPage_Saved_WithXp_Format,
                    display.ToString("0.0", CultureInfo.CurrentCulture),
                    unitLabel,
                    result.XpAwarded)
                : string.Format(AppResources.CheckInPage_Saved_NoXp_Format,
                    display.ToString("0.0", CultureInfo.CurrentCulture),
                    unitLabel);
        }
    }
}
