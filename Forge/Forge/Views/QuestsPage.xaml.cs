using Forge.Resources.Strings;
using Forge.ViewModels;

namespace Forge.Views
{
    public partial class QuestsPage : ContentPage
    {
        private readonly QuestsViewModel _vm;
        private bool _initialized;

        public QuestsPage(QuestsViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // ensure single subscription
            _vm.DailyXpAwarded -= OnDailyXpAwarded;
            _vm.DailyXpAwarded += OnDailyXpAwarded;

            // prevent re-entrant init on repeated navigations
            if (_initialized || _vm.IsBusy) return;

            try
            {
                await _vm.InitializeAsync();
                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Quests init failed: {ex}");
                if (this.Window is not null) // avoid showing alerts when page not visible
                    await DisplayAlert("Error", "Failed to load quests. Please try again.", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm.DailyXpAwarded -= OnDailyXpAwarded;
        }

        private async void OnDailyXpAwarded(object? sender, int xp)
        {
            var title = AppResources.QuestPage_XpDialog_Title;
            var message = string.Format(AppResources.QuestPage_XpDialog_Message_Format, $"+{xp}");
            var ok = AppResources.Common_OK;
            
            await DisplayAlert(title, message, ok);
        }
    }
}
