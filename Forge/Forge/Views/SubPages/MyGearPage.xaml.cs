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
        if (_vm.Gear.Count == 0)
            await _vm.LoadAsync();
    }

    /// <summary>Owned dumbbell / kettlebell / barbell weights (Decision 16).</summary>
    private async void OnWeightsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(HomeWeightsPage));
}