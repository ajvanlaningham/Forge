using Forge.ViewModels;

namespace Forge.Views;

/// <summary>Before a guided session: look over the plan, settle weights, gather gear, then BEGIN.</summary>
public partial class BriefingPage : ContentPage
{
    private readonly BriefingViewModel _vm;
    private bool _loaded;

    public BriefingPage(BriefingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return; // keep the weights chosen if the user pops back from My Gear
        _loaded = true;
        try { await _vm.LoadAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Briefing load failed: {ex}");
            await DisplayAlertAsync("Can't start", ex.Message, "OK");
            await Shell.Current.GoToAsync("..");
        }
    }
}
