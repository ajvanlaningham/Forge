namespace Forge
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnBeginTrainingClicked(object sender, EventArgs e)
        {
            // Switch to the Train tab
            await Shell.Current.GoToAsync("//train");
        }
    }

}
