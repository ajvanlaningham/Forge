using Forge.ViewModels;

namespace Forge.Views
{
    public partial class QuestsPage : ContentPage
    {
        private readonly QuestsViewModel _vm;

        public QuestsPage(QuestsViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            _vm.DailyXpAwarded -= OnDailyXpAwarded;
            _vm.DailyXpAwarded += OnDailyXpAwarded;

            await _vm.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm.DailyXpAwarded -= OnDailyXpAwarded;
        }

        private async void OnDailyXpAwarded(object? sender, int xp)
        {
            await DisplayAlert("Nice!", $"+{xp} XP earned", "OK");
            await Shell.Current.GoToAsync("//HomePage");
        }
    }
}
