using Forge.ViewModels;

namespace Forge.Views;

/// <summary>The Guild tab: the quest board where training programs are chosen.</summary>
public partial class GuildPage : ContentPage
{
    private readonly QuestBoardViewModel _vm;

    public GuildPage(QuestBoardViewModel vm)
    {
        Resources.Add("InvertBool", new InvertBoolConverter());
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Quest board load failed: {ex}"); }
    }

    private sealed class InvertBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : true;
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
