using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Forge
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
        {
            // The backup restore picker (PBI 10.2) is launched directly so it can open in Download/Forge.
            if (Forge.Platforms.Android.BackupFilePicker.HandleResult(requestCode, resultCode, data)) return;
            base.OnActivityResult(requestCode, resultCode, data);
        }
    }
}
