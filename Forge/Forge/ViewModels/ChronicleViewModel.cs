using System.Windows.Input;
using Forge.ViewModels.Controls.Cards;
using Forge.Views;

namespace Forge.ViewModels
{
    /// <summary>The Chronicle tab (Decision 20): looking back. Weigh-ins and finished sessions.</summary>
    public sealed class ChronicleViewModel : BaseViewModel
    {
        public WeightTrendCardViewModel WeightTrend { get; }
        public SessionHistoryViewModel History { get; }
        public ICommand WeighInCommand { get; } = new Command(async () => await Shell.Current.GoToAsync(nameof(CheckInPage)));

        public ChronicleViewModel(WeightTrendCardViewModel weightTrend, SessionHistoryViewModel history)
        {
            WeightTrend = weightTrend;
            History = history;
            Title = "Chronicle";
        }

        public async Task RefreshAsync()
        {
            await WeightTrend.RefreshAsync();
            await History.LoadAsync();
        }
    }
}
