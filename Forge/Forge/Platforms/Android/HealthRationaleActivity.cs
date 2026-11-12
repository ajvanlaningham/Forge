using Android.App;
using Android.OS;
using Android.Widget;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Health Connect requires an app to explain what it does with health data before it will
    /// show the permission screen; the "privacy policy" link on that screen opens this. The
    /// fixed Java name lets AndroidManifest.xml point its activity-alias here.
    /// </summary>
    [Activity(Name = "io.github.ajvanlaningham.forge.HealthRationaleActivity", Exported = true, Theme = "@style/Maui.MainTheme")]
    [IntentFilter(new[] { "androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE" })]
    public sealed class HealthRationaleActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var pad = (int)(24 * (Resources?.DisplayMetrics?.Density ?? 2));
            var text = new global::Android.Widget.TextView(this) { TextSize = 16 };
            text.SetPadding(pad, pad * 2, pad, pad);
            text.Text =
                "How Forge uses your health data\n\n" +
                "Forge reads your resting heart rate, sleep and steps from Health Connect to fill in " +
                "your daily check-in. It only reads; it never writes or deletes anything in Health Connect.\n\n" +
                "The numbers are stored only in Forge's database on this phone and in its own backups " +
                "(Download/Forge). They are never sent anywhere. Forge has no server and no account.\n\n" +
                "Anything you type into the check-in yourself is never overwritten.\n\n" +
                "You can turn access off at any time in Health Connect's settings.";
            var scroll = new global::Android.Widget.ScrollView(this);
            scroll.AddView(text);
            SetContentView(scroll);
        }
    }
}
