using Forge.Constants;

namespace Forge.Services.Interfaces
{
    public enum HealthAvailability
    {
        /// <summary>This platform has no health store Forge can read (yet).</summary>
        Unsupported,
        /// <summary>The health store app is missing or needs an update.</summary>
        NeedsInstall,
        Available,
    }

    /// <summary>
    /// The phone's health store: Health Connect on Android, Apple Health on iOS (later).
    /// Read-only. Everything here is optional to the app: when it's unavailable or permission
    /// is refused, the check-in simply stays manual.
    /// </summary>
    public interface IHealthData
    {
        /// <summary>"Health Connect", "Apple Health".</summary>
        string StoreName { get; }

        HealthAvailability Availability { get; }

        /// <summary>True when at least one of the reads Forge asks for has been granted.</summary>
        Task<bool> HasAnyPermissionAsync();

        /// <summary>Shows the system permission screen. Returns whether anything was granted.</summary>
        Task<bool> RequestPermissionsAsync();

        /// <summary>Opens the store's own settings (to review or revoke access, or install it).</summary>
        void OpenSettings();

        /// <summary>The day's resting HR, sleep and steps. Fields the store doesn't have are null.</summary>
        Task<HealthDay> ReadDayAsync(DateOnly date);
    }

    /// <summary>Platforms without a health store: everything stays manual.</summary>
    public sealed class NoHealthData : IHealthData
    {
        public string StoreName => "Health data";
        public HealthAvailability Availability => HealthAvailability.Unsupported;
        public Task<bool> HasAnyPermissionAsync() => Task.FromResult(false);
        public Task<bool> RequestPermissionsAsync() => Task.FromResult(false);
        public void OpenSettings() { }
        public Task<HealthDay> ReadDayAsync(DateOnly date) => Task.FromResult(new HealthDay(null, null, null));
    }
}
