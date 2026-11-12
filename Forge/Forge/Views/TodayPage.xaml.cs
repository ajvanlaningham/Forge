using Forge.ViewModels;

namespace Forge.Views;

public partial class TodayPage : ContentPage
{
    private readonly TodayViewModel _vm;

    public TodayPage(TodayViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        ConditioningCard.ViewModel = vm.Conditioning;
        TabChrome.AddSettingsCog(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.XpAwarded -= OnXpAwarded;
        _vm.XpAwarded += OnXpAwarded;
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Today load failed: {ex}"); }
        await CheckLevelUpAsync();
        await ShowForgingIfDueAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.XpAwarded -= OnXpAwarded;
    }

    private async void OnXpAwarded(object? sender, int xp)
    {
        await DisplayAlertAsync("Active rest", $"+{xp} XP", "OK");
        await CheckLevelUpAsync();
    }

    private bool _forging;

    /// <summary>A block whose four weeks are up is judged here, and its forging moment shown once.</summary>
    private async Task ShowForgingIfDueAsync()
    {
        if (_forging) return;
        var services = IPlatformApplication.Current?.Services;
        if (services?.GetService<Forge.Services.Interfaces.IForgeService>() is not { } forge
            || services.GetService<Forge.Services.Interfaces.IProgramService>() is not { } programs) return;
        try
        {
            _forging = true;
            if (await forge.JudgeIfDueAsync(DateOnly.FromDateTime(DateTime.Today)) is { } result)
            {
                await forge.MarkSeenAsync(result.Id);
                await Navigation.PushModalAsync(new ForgingPage(result, programs));
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Forge judgment failed: {ex}"); }
        finally { _forging = false; }
    }

    private static async Task CheckLevelUpAsync()
    {
        var progress = IPlatformApplication.Current?.Services.GetService<Forge.Services.ProgressService>();
        if (progress is not null) await progress.CheckLevelUpAsync();
    }
}
