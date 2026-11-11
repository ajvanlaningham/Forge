using Forge.Views;
using Forge.Views.SubPages;

namespace Forge
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ExerciseLibraryPage), typeof(ExerciseLibraryPage));
            Routing.RegisterRoute(nameof(MyGearPage), typeof(MyGearPage));
            Routing.RegisterRoute(nameof(ActiveSessionPage), typeof(ActiveSessionPage));
            Routing.RegisterRoute(nameof(SessionSummaryPage), typeof(SessionSummaryPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(CheckInPage), typeof(CheckInPage));
            Routing.RegisterRoute(nameof(ExerciseHistoryPage), typeof(ExerciseHistoryPage));
            Routing.RegisterRoute(nameof(GymEquipmentPage), typeof(GymEquipmentPage));
            Routing.RegisterRoute(nameof(HomeWeightsPage), typeof(HomeWeightsPage));

        }
    }
}
