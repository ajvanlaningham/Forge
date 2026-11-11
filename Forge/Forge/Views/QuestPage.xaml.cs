using Forge.ViewModels;

namespace Forge.Views;

/// <summary>The Quest tab (Decision 20): the quest underway, then the quest board.</summary>
public partial class QuestPage : ContentPage
{
    private readonly QuestBoardViewModel _vm;
    private readonly CurrentQuestViewModel _current;

    public QuestPage(QuestBoardViewModel vm, CurrentQuestViewModel current)
    {
        Resources.Add("InvertBool", new InvertBoolConverter());
        InitializeComponent();
        BindingContext = _vm = vm;
        CurrentQuest.BindingContext = _current = current;
        // Accepting a quest on the board changes the one underway.
        _vm.QuestAccepted += async (_, _) => await _current.RefreshAsync();
        TabChrome.AddSettingsCog(this);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _current.RefreshAsync();
            await _vm.LoadAsync();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Quest tab load failed: {ex}"); }
    }

    private sealed class InvertBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : true;
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
