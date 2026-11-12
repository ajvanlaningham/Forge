namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Posts finished strength sessions to Strava as Weight Training activities. Sessions
    /// finished after connecting queue up and post whenever <see cref="SyncAsync"/> runs
    /// (after a session, and when the app comes to the foreground), so being offline only
    /// delays a post.
    /// </summary>
    public interface IStravaService
    {
        /// <summary>False until the Forge API app's client ID is filled in (StravaAuth.ClientId).</summary>
        bool IsAvailable { get; }

        bool IsConnected { get; }

        /// <summary>The athlete's name as Strava gave it, once connected.</summary>
        string? AthleteName { get; }

        /// <summary>What happened on the last sync, for Settings ("Posted 1 session", or the error).</summary>
        string LastResult { get; }

        /// <summary>Whether this phone has the API app's client secret (pasted once, kept in secure storage).</summary>
        Task<bool> HasSecretAsync();

        /// <summary>Stores a pasted client secret. False when it doesn't look like one (40 hex characters).</summary>
        Task<bool> SaveSecretAsync(string? pasted);

        /// <summary>Runs Strava's sign-in. Returns a message for the user.</summary>
        Task<string> ConnectAsync();

        Task DisconnectAsync();

        Task<int> PendingCountAsync();

        /// <summary>Posts everything pending. Never throws; one sync at a time.</summary>
        Task SyncAsync();
    }
}
