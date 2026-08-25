using Forge.ViewModels;

namespace Forge.Views;

public partial class CheckInPage : ContentPage
{
    private readonly CheckInViewModel _vm;

    public CheckInPage(CheckInViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }
}
