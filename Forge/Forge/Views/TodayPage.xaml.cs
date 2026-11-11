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
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.XpAwarded -= OnXpAwarded;
    }

    private async void OnXpAwarded(object? sender, int xp)
    {
        await DisplayAlert("Active rest", $"+{xp} XP", "OK");
        await CheckLevelUpAsync();
    }

    private static async Task CheckLevelUpAsync()
    {
        var progress = IPlatformApplication.Current?.Services.GetService<Forge.Services.ProgressService>();
        if (progress is not null) await progress.CheckLevelUpAsync();
    }
}
