using Forge.Services.Interfaces;
using Forge.ViewModels;

namespace Forge.Views;

[QueryProperty(nameof(SessionId), "sessionId")]
public partial class GuidedSessionPage : ContentPage
{
    private readonly GuidedSessionViewModel _vm;

    public int SessionId { get; set; }

    public GuidedSessionPage(GuidedSessionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Ended += OnEnded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true; // it's on the floor between sets
        try
        {
            // Always reload: the list view may have logged, skipped or swapped something.
            await _vm.LoadAsync(SessionId);
            _vm.StartClock();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Guided session load failed: {ex}");
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
