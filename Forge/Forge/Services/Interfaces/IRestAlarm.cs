namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Fires an alert when a rest period ends, even with the screen off or the app in the
    /// background (PBI 8.3). Android schedules an exact alarm that posts a vibrating notification;
    /// other platforms get a no-op until they are built (iOS will use a local notification).
    /// </summary>
    public interface IRestAlarm
    {
        /// <summary>
        /// Asks for notification permission if it hasn't been decided yet. Returns whether alerts
        /// can be shown; when false, the app vibrates in-app instead (foreground only).
        /// </summary>
        Task<bool> EnsurePermissionAsync();

        /// <summary>True when background alerts are possible right now.</summary>
        bool CanNotify { get; }

        /// <summary>Schedules (or reschedules) the alert for <paramref name="endsAtUtc"/>.</summary>
        void Schedule(DateTime endsAtUtc, string message);

        void Cancel();
    }

    public sealed class NoopRestAlarm : IRestAlarm
    {
        public Task<bool> EnsurePermissionAsync() => Task.FromResult(false);
        public bool CanNotify => false;
        public void Schedule(DateTime endsAtUtc, string message) { }
        public void Cancel() { }
    }
}
