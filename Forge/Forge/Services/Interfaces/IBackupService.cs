namespace Forge.Services.Interfaces
{
    public sealed record BackupResult(bool Success, string? FileName, string Message);

    /// <summary>
    /// Daily database backup (PBI 10.1). Copies are written where uninstalling the app does not
    /// delete them, because the scenario that matters — a forced reinstall — is exactly the one
    /// that wipes app-private storage.
    /// </summary>
    public interface IBackupService
    {
        DateTime? LastBackupUtc { get; }

        /// <summary>Human-readable place backups go, e.g. "Download/Forge".</summary>
        string Location { get; }

        /// <summary>Backs up if none has been made today. Never throws; never shows UI.</summary>
        Task BackUpIfDueAsync();

        /// <summary>Backs up now regardless of the schedule.</summary>
        Task<BackupResult> BackUpNowAsync();

        /// <summary>
        /// Checks that a picked file is a Forge backup this version can restore. Copies it to a
        /// staging file first; nothing live is touched. Returns the staged path on success.
        /// </summary>
        Task<RestoreCheck> InspectAsync(Stream picked, string fileName);

        /// <summary>
        /// Replaces the live database with a staged backup from <see cref="InspectAsync"/>, after
        /// backing up the current data. The app must restart afterwards.
        /// </summary>
        Task<BackupResult> RestoreAsync(RestoreCheck check);
    }

    public sealed record RestoreCheck(bool Ok, string Message, string? StagedPath, int Schema, string Summary);

    /// <summary>Where backup files land. Platform-specific: shared storage on Android.</summary>
    public interface IBackupSink
    {
        string Location { get; }
        Task WriteAsync(string sourcePath, string fileName);
        /// <summary>Backup files this install can see (and so prune).</summary>
        Task<IReadOnlyList<string>> ListAsync();
        Task DeleteAsync(string fileName);
    }

    /// <summary>Fallback sink in app storage. Does NOT survive uninstall; used off-Android only.</summary>
    public sealed class AppDataBackupSink : IBackupSink
    {
        private static string Dir => Path.Combine(FileSystem.AppDataDirectory, "backups");
        public string Location => Dir;

        public Task WriteAsync(string sourcePath, string fileName)
        {
            Directory.CreateDirectory(Dir);
            File.Copy(sourcePath, Path.Combine(Dir, fileName), overwrite: true);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync()
            => Task.FromResult<IReadOnlyList<string>>(Directory.Exists(Dir)
                ? Directory.GetFiles(Dir).Select(Path.GetFileName).OfType<string>().ToList()
                : new List<string>());

        public Task DeleteAsync(string fileName)
        {
            File.Delete(Path.Combine(Dir, fileName));
            return Task.CompletedTask;
        }
    }
}
