using Forge.ViewModels.SubPages;

namespace Forge.Views.SubPages;

public partial class ExerciseLibraryPage : ContentPage
{
    private readonly ExerciseLibraryViewModel _vm;

    public ExerciseLibraryPage(ExerciseLibraryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.Items.Count == 0)
            await _vm.LoadAsync();
    }
}