using Forge.ViewModels;

namespace Forge.Views;

[QueryProperty(nameof(Exercise), "exercise")]
public partial class ExerciseHistoryPage : ContentPage
{
    private readonly ExerciseHistoryViewModel _vm;

    /// <summary>Exercise name (URL-decoded by Shell).</summary>
    public string Exercise { get; set; } = "";

    public ExerciseHistoryPage(ExerciseHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(Uri.UnescapeDataString(Exercise)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Exercise history load failed: {ex}"); }
    }

    /// <summary>Navigates here for an exercise.</summary>
    public static Task OpenAsync(string exerciseName)
        => Shell.Current.GoToAsync($"{nameof(ExerciseHistoryPage)}?exercise={Uri.EscapeDataString(exerciseName)}");
}
