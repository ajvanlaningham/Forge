using Forge.ViewModels;

namespace Forge.Views;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class SessionSummaryPage : ContentPage
{
    private readonly SessionSummaryViewModel _vm;

    public int SessionId { get; set; }

    public SessionSummaryPage(SessionSummaryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _vm.LoadAsync(SessionId);
            // A level reached in this session shows as a banner here, not as another screen.
            var progress = IPlatformApplication.Current?.Services.GetService<Forge.Services.ProgressService>();
            if (progress is not null && await progress.ClaimLevelUpAsync() is int level)
                _vm.LevelBanner = $"LEVEL {level}!";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Summary load failed: {ex}");
        }
    }

    private async void OnDone(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
