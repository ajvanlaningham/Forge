namespace Forge.Views;

public partial class HomePage : ContentPage
{
	public HomePage()
	{
		InitializeComponent();
	}

    private async void OnBeginTrainingClicked(object sender, EventArgs e)
    {
        // Switch to the Train tab
        await Shell.Current.GoToAsync("//train");
    }
}