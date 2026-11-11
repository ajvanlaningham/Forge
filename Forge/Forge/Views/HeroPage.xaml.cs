using Forge.ViewModels;

namespace Forge.Views;

public partial class HeroPage : ContentPage
{
    private readonly HeroViewModel _vm;

    public HeroPage(HeroViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        Stats.ViewModel = vm.StatsCard;
        TabChrome.AddSettingsCog(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Hero load failed: {ex}"); }
    }
}
