using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Receives Strava's sign-in redirect (forgestrava://localhost/strava?code=…) and hands it
    /// back to WebAuthenticator. The scheme must match <see cref="Forge.Constants.StravaAuth.RedirectUri"/>.
    /// </summary>
    [Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
    [IntentFilter(new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "forgestrava")]
    public sealed class StravaCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
    {
    }
}
