namespace Forge.Services.Interfaces
{
    /// <summary>
    /// End-to-end encrypted backups to nidavellir (Decision 24). The phone encrypts with a key
    /// derived from a recovery key the user keeps (e.g. in their password manager); the server
    /// stores ciphertext it can't read. Restoring on a new phone needs only the recovery key.
    /// </summary>
    public interface IOffsiteBackupService
    {
        bool IsSetUp { get; }
        DateTime? LastUploadUtc { get; }
        string LastResult { get; }

        /// <summary>Creates a recovery key and uploads the first backup. Returns the key to show once, written out.</summary>
        Task<string> SetUpAsync();

        /// <summary>The recovery key, written out, for showing again to the person holding this phone.</summary>
        Task<string?> RecoveryKeyAsync();

        /// <summary>Encrypts and uploads now. Returns a message for the user.</summary>
        Task<string> UploadNowAsync();

        /// <summary>Uploads if the last upload is a day old or more. Never throws.</summary>
        Task UploadIfDueAsync();

        /// <summary>Stops off-phone backups and deletes the copy on the server.</summary>
        Task<string> TurnOffAsync();

        /// <summary>
        /// Downloads and decrypts the backup for a recovery key into a temporary database file.
        /// Returns the file's path, or an error message.
        /// </summary>
        Task<(string? Path, string? Error)> DownloadAsync(string recoveryKey);
    }
}
