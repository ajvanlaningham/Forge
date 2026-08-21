using System.Net.Http.Json;
using System.Text.Json;

using Forge.Constants;
using Forge.Models;
using Forge.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Forge.Services.Implementations
{
    /// <inheritdoc />
    public sealed class UpdateService : IUpdateService
    {
        private readonly ILogger<UpdateService> _log;

        // The APK runs to tens of megabytes, so this needs a far longer ceiling than an
        // ordinary REST call would.
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

        public UpdateService(ILogger<UpdateService> log)
        {
            _log = log;
            // GitHub rejects API requests that arrive without a User-Agent.
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(GameConstants.Updates.UserAgent);
        }

        public long InstalledVersionCode
        {
            get
            {
                // AppInfo.Current can throw if touched before MAUI essentials are initialised.
                // Treat that as "unknown build" rather than taking the app down.
                try
                {
                    return long.TryParse(AppInfo.Current.BuildString, out var code) ? code : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string InstalledVersionName
        {
            get
            {
                try
                {
                    return AppInfo.Current.VersionString ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

#if ANDROID
        public bool CanInstallPackages
        {
            get
            {
                if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O) return true;
                return Android.App.Application.Context.PackageManager?.CanRequestPackageInstalls() ?? false;
            }
        }

        public void RequestInstallPermission()
        {
            var context = Android.App.Application.Context;
            var intent = new Android.Content.Intent(
                Android.Provider.Settings.ActionManageUnknownAppSources,
                Android.Net.Uri.Parse("package:" + context.PackageName));
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
#else
        public bool CanInstallPackages => false;

        public void RequestInstallPermission() =>
            throw new PlatformNotSupportedException("In-app APK install is Android-only.");
#endif

        public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
        {
            var installed = InstalledVersionCode;

            try
            {
                var manifest = await FetchManifestAsync(ct);

                if (manifest is null)
                    return new UpdateCheckResult(false, installed, null, "No build published yet.");

                if (manifest.VersionCode > installed)
                {
                    return new UpdateCheckResult(true, installed, manifest,
                        $"Update available: {manifest.VersionName} (build {manifest.VersionCode}).");
                }

                return new UpdateCheckResult(false, installed, manifest,
                    $"Up to date (build {installed}).");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning(ex, "Update check could not reach GitHub");
                return new UpdateCheckResult(false, installed, null, "Couldn't reach GitHub. Check your connection.");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Update check failed");
                return new UpdateCheckResult(false, installed, null, "Update check failed.");
            }
        }

        public async Task DownloadAndInstallAsync(IProgress<double>? progress = null, CancellationToken ct = default)
        {
            var release = await FetchLatestReleaseAsync(ct)
                ?? throw new InvalidOperationException("No release is published.");

            var apk = release.Assets.FirstOrDefault(
                a => a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The latest release has no APK attached.");

            var dest = Path.Combine(FileSystem.CacheDirectory, GameConstants.Updates.DownloadFileName);

            using (var resp = await _http.GetAsync(
                apk.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? apk.Size;

                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(dest);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress?.Report((double)read / total);
                }
            }

            await InstallApkAsync(dest);
        }

        // --- helpers ---

        private async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct)
        {
            using var resp = await _http.GetAsync(GameConstants.Updates.LatestReleaseUrl, ct);

            // A repo with no releases yet answers 404. That is "nothing published", not a fault.
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: ct);
        }

        private async Task<ReleaseManifest?> FetchManifestAsync(CancellationToken ct)
        {
            var release = await FetchLatestReleaseAsync(ct);
            if (release is null) return null;

            var asset = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, GameConstants.Updates.ManifestAssetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null)
            {
                _log.LogWarning("Release {Tag} has no {Asset} attached", release.TagName,
                    GameConstants.Updates.ManifestAssetName);
                return null;
            }

            try
            {
                return await _http.GetFromJsonAsync<ReleaseManifest>(asset.BrowserDownloadUrl, ct);
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex, "Release manifest was not valid JSON");
                return null;
            }
        }

#if ANDROID
        private static Task InstallApkAsync(string filePath)
        {
            var context = Android.App.Application.Context;
            var file = new Java.IO.File(filePath);
            // Authority must match the <provider> in AndroidManifest.xml.
            var authority = context.PackageName + ".fileprovider";
            var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, file);

            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            // NewTask: started from a non-Activity context. GrantReadUriPermission: the package
            // installer is a different app and must be allowed to read the content URI.
            intent.AddFlags(Android.Content.ActivityFlags.NewTask
                          | Android.Content.ActivityFlags.GrantReadUriPermission);
            context.StartActivity(intent);
            return Task.CompletedTask;
        }
#else
        private static Task InstallApkAsync(string filePath) =>
            throw new PlatformNotSupportedException("In-app APK install is Android-only.");
#endif
    }
}
