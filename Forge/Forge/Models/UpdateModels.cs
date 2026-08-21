using System.Text.Json.Serialization;

namespace Forge.Models
{
    /// <summary>
    /// The <c>version.json</c> asset published alongside each release APK by CI.
    /// Shape is defined by <c>.github/workflows/android-release.yml</c>; keep the two in step.
    /// </summary>
    public sealed record ReleaseManifest
    {
        [JsonPropertyName("version_name")] public string VersionName { get; init; } = string.Empty;

        /// <summary>
        /// Android <c>versionCode</c>, set from the CI run number so it increases monotonically.
        /// This single integer is the whole update comparison.
        /// </summary>
        [JsonPropertyName("version_code")] public long VersionCode { get; init; }

        [JsonPropertyName("sha")] public string Sha { get; init; } = string.Empty;
        [JsonPropertyName("built_at")] public string BuiltAt { get; init; } = string.Empty;
        [JsonPropertyName("notes")] public string Notes { get; init; } = string.Empty;

        /// <summary>The APK asset's filename, for display. The download URL comes from the release.</summary>
        [JsonPropertyName("apk")] public string Apk { get; init; } = string.Empty;
    }

    /// <summary>Outcome of an update check.</summary>
    /// <param name="UpdateAvailable">True when the published version code exceeds the installed one.</param>
    /// <param name="InstalledVersionCode">The build currently installed, or 0 if unknown.</param>
    /// <param name="Latest">The published manifest, or null if nothing is published or GitHub was unreachable.</param>
    /// <param name="Message">Short human-readable status for the Settings screen.</param>
    public sealed record UpdateCheckResult(
        bool UpdateAvailable,
        long InstalledVersionCode,
        ReleaseManifest? Latest,
        string Message);

    // --- GitHub Releases API shapes (only the fields actually used) ---

    internal sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; init; } = string.Empty;
        [JsonPropertyName("html_url")] public string HtmlUrl { get; init; } = string.Empty;
        [JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; init; } = new();
    }

    internal sealed record GitHubReleaseAsset
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; init; } = string.Empty;
        [JsonPropertyName("size")] public long Size { get; init; }
    }
}
