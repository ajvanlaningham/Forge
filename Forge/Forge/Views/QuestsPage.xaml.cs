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
            await _vm.InitializeAsync();
        }
    }
}
