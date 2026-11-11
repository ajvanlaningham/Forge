using Forge.ViewModels;

namespace Forge.Views;

public partial class ChroniclePage : ContentPage
{
    private readonly ChronicleViewModel _vm;

    public ChroniclePage(ChronicleViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        Trend.BindingContext = vm.WeightTrend;
        Sessions.BindingContext = vm.History;
        TabChrome.AddSettingsCog(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Chronicle load failed: {ex}"); }
    }

    private void OnSessionTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is SessionHistoryItem item)
            _vm.History.OpenCommand.Execute(item.SessionId);
    }
}
