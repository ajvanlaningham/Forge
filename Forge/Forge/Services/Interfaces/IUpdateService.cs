using Forge.Models;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// In-app self-update for the Android build. Reads the manifest published with the latest
    /// GitHub Release, compares it to the installed build, and — when newer — downloads the
    /// release-signed APK and hands it to the OS package installer.
    /// </summary>
    /// <remarks>
    /// <para>Android-only: <see cref="DownloadAndInstallAsync"/> throws elsewhere.</para>
    /// <para>
    /// A release-signed APK will NOT install over a debug build from <c>deploy-android.sh</c> —
    /// the signatures differ, and Android refuses. The first release install needs a manual
    /// uninstall, which deletes the local database.
    /// </para>
    /// </remarks>
    public interface IUpdateService
    {
        /// <summary>
        /// The installed build's Android <c>versionCode</c>, or 0 when it cannot be determined.
        /// Equals the CI run number that produced the build.
        /// </summary>
        long InstalledVersionCode { get; }

        /// <summary>Installed version name, for display. Empty when unavailable.</summary>
        string InstalledVersionName { get; }

        /// <summary>
        /// Fetch the latest published manifest and compare it to the installed build.
        /// Never throws: failures come back as a result carrying a readable message.
        /// </summary>
        Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default);

        /// <summary>
        /// Whether the OS will let this app install packages. On API 26+ the user must grant
        /// "install unknown apps" for Forge specifically; without it the install intent is
        /// refused after the download has already happened.
        /// </summary>
        bool CanInstallPackages { get; }

        /// <summary>Send the user to the system screen where that permission is granted.</summary>
        void RequestInstallPermission();

        /// <summary>
        /// Download the latest APK to the app cache and launch the OS package installer.
        /// Android-only.
        /// </summary>
        /// <param name="progress">0.0–1.0 download progress; not reported if length is unknown.</param>
        Task DownloadAndInstallAsync(IProgress<double>? progress = null, CancellationToken ct = default);
    }
}
