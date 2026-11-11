using Forge.ViewModels;

namespace Forge.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
        await CheckLevelUpAsync();
    }

    private static async Task CheckLevelUpAsync()
    {
        var progress = IPlatformApplication.Current?.Services.GetService<Forge.Services.ProgressService>();
        if (progress is not null) await progress.CheckLevelUpAsync();
    }
}
