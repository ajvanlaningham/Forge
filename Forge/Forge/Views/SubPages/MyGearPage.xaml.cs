using Forge.ViewModels.SubPages;

namespace Forge.Views.SubPages;

public partial class MyGearPage : ContentPage
{
    private readonly MyGearViewModel _vm;

    public MyGearPage(MyGearViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Always reload: an editor may have changed ownership or weights.
        await _vm.LoadAsync();
    }
}