using Android.App;
using Android.Content.PM;
using Android.OS;

using AndroidX.Activity.Result;
using AndroidX.Health.Connect.Client;

namespace Forge
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static MainActivity? _current;
        private ActivityResultLauncher? _healthPermissions;
        private static TaskCompletionSource? _healthPending;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            _current = this;
            // Activity-result launchers must be registered before the activity starts, so the
            // Health Connect permission screen is wired up here and launched on demand.
            try
            {
                _healthPermissions = RegisterForActivityResult(
                    IPermissionController.CreateRequestPermissionResultContract(),
                    new HealthPermissionCallback());
            }
            catch (Exception ex) { Android.Util.Log.Warn("Forge", $"Health Connect unavailable: {ex.Message}"); }
        }

        protected override void OnDestroy()
        {
            if (_current == this) _current = null;
            base.OnDestroy();
        }

        /// <summary>Shows Health Connect's permission screen; completes when it closes.</summary>
        public static Task RequestHealthPermissionsAsync(IEnumerable<string> permissions)
        {
            if (_current?._healthPermissions is not { } launcher) return Task.CompletedTask;
            _healthPending?.TrySetResult();
            _healthPending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var set = new Java.Util.HashSet();
            foreach (var p in permissions) set.Add(new Java.Lang.String(p));
            launcher.Launch(set);
            return _healthPending.Task;
        }

        private sealed class HealthPermissionCallback : Java.Lang.Object, IActivityResultCallback
        {
            public void OnActivityResult(Java.Lang.Object? result) => _healthPending?.TrySetResult();
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
        {
            // The backup restore picker (PBI 10.2) is launched directly so it can open in Download/Forge.
            if (Forge.Platforms.Android.BackupFilePicker.HandleResult(requestCode, resultCode, data)) return;
            base.OnActivityResult(requestCode, resultCode, data);
        }
    }
}
