using Forge.Views.SubPages;

namespace Forge.Views;

public partial class TrainPage : ContentPage
{
	public TrainPage()
	{
		InitializeComponent();
	}
    private async void OnOpenLibraryClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ExerciseLibraryPage));
    }
}