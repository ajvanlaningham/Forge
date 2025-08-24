using Forge.Views.SubPages;

namespace Forge
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ExerciseLibraryPage), typeof(ExerciseLibraryPage));
        }
    }
}
