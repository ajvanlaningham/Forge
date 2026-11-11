using Forge.ViewModels;

namespace Forge.Views;

public partial class SessionHistoryPage : ContentPage
{
    private readonly SessionHistoryViewModel _vm;

    public SessionHistoryPage(SessionHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"History load failed: {ex}"); }
    }
}
