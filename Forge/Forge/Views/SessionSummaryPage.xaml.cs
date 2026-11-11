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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Summary load failed: {ex}");
        }
    }

    private async void OnDone(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
