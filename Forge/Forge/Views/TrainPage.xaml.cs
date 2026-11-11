using Forge.ViewModels;

namespace Forge.Views;

public partial class TrainPage : ContentPage
{
    private readonly TrainViewModel _vm;

    public TrainPage(TrainViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Refresh every time: completing a quest elsewhere moves the program on.
            await _vm.RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Train refresh failed: {ex}");
            await DisplayAlert("Error", "Couldn't load your program.", "OK");
        }
    }
}
