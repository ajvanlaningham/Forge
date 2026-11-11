using Forge.Services.Interfaces;
using Forge.ViewModels;

namespace Forge.Views;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class ActiveSessionPage : ContentPage
{
    private readonly ActiveSessionViewModel _vm;
    private int _loadedFor = -1;

    public int SessionId { get; set; }

    public ActiveSessionPage(ActiveSessionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Ended += OnEnded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true; // it's on a bench between sets
        try
        {
            if (_loadedFor != SessionId)
            {
                await _vm.LoadAsync(SessionId);
                _loadedFor = SessionId;
            }
            _vm.StartClock();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Session load failed: {ex}");
            await DisplayAlert("Error", "Couldn't load this session.", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DeviceDisplay.Current.KeepScreenOn = false;
        _vm.StopClock();
    }

    private async void OnEnded(object? sender, SessionSnapshot? finished)
    {
        await Shell.Current.GoToAsync("..");
        if (finished is not null)
            await Shell.Current.GoToAsync($"{nameof(SessionSummaryPage)}?sessionId={finished.Session.Id}");
    }
}
