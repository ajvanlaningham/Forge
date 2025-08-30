using System.Windows.Input;

namespace Forge.ViewModels.Controls.Cards
{
    public sealed class WeeklyConditioningCardViewModel : BaseViewModel
    {
        private string _title = "Conditioning (Weekly)";
        private string _weekRangeText = string.Empty;
        private int _minutes;
        private int _goalMinutes = 180;
        private string _hintText = string.Empty;
        private bool _showHint;
        private ICommand? _addMinutesCommand;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string WeekRangeText
        {
            get => _weekRangeText;
            set => SetProperty(ref _weekRangeText, value);
        }

        public int Minutes
        {
            get => _minutes;
            set
            {
                if (SetProperty(ref _minutes, Math.Max(0, value)))
                {
                    // Fire dependent properties
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressLabel));
                    OnPropertyChanged(nameof(CompletedStatusText));
                }
            }
        }

        public int GoalMinutes
        {
            get => _goalMinutes;
            set
            {
                if (SetProperty(ref _goalMinutes, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressLabel));
                    OnPropertyChanged(nameof(CompletedStatusText));
                }
            }
        }

        /// <summary>0..1 ratio for a ProgressBar.</summary>
        public double Progress => GoalMinutes <= 0 ? 0.0 : Math.Clamp((double)Minutes / GoalMinutes, 0.0, 1.0);

        public string ProgressLabel => $"{Minutes} / {GoalMinutes} min";

        public string CompletedStatusText => Minutes >= GoalMinutes ? "Completed ✅" : string.Empty;

        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        public bool ShowHint
        {
            get => _showHint;
            set => SetProperty(ref _showHint, value);
        }

        public ICommand? AddMinutesCommand
        {
            get => _addMinutesCommand;
            set => SetProperty(ref _addMinutesCommand, value);
        }
    }
}
