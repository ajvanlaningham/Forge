using Forge.ViewModels;

namespace Forge.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // The daily backup runs in the background on launch; show its latest result.
        _vm.RefreshBackupStatus();
        _ = _vm.RefreshStravaAsync();
        _ = _vm.RefreshHealthAsync();
        _vm.RefreshOffsite();
    }
}
