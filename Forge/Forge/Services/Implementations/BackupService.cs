using System.Globalization;
using Forge.Constants;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    public sealed class BackupService : IBackupService
    {
        private const string LastBackupKey = "Backup.LastUtcTicks";

        private readonly IAppDatabase _db;
        private readonly IBackupSink _sink;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public BackupService(IAppDatabase db, IBackupSink sink)
        {
            _db = db;
            _sink = sink;
        }

        public DateTime? LastBackupUtc
        {
            get
            {
                var ticks = Preferences.Get(LastBackupKey, 0L);
                return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
            }
            private set => Preferences.Set(LastBackupKey, value?.Ticks ?? 0L);
        }

        public string Location => _sink.Location;

        public async Task BackUpIfDueAsync()
        {
            try
            {
                if (!BackupPolicy.IsDue(LastBackupUtc, DateTime.UtcNow)) return;
                var result = await BackUpNowAsync();
                System.Diagnostics.Debug.WriteLine($"Scheduled backup: {result.Message}");
            }
            catch (Exception ex)
            {
                // Never block or interrupt the app over a backup. Tomorrow's launch retries.
                System.Diagnostics.Debug.WriteLine($"Scheduled backup failed: {ex}");
            }
        }

        public async Task<BackupResult> BackUpNowAsync()
        {
            if (!await _gate.WaitAsync(0))
                return new BackupResult(false, null, "A backup is already running.");
            var temp = Path.Combine(FileSystem.CacheDirectory, $"backup-{Guid.NewGuid():N}.db3");
            try
            {
                // VACUUM INTO writes a complete, consistent copy through SQLite itself. A byte copy
                // of an open database file can capture a torn write and restore as corruption.
                await _db.Connection.ExecuteAsync("VACUUM INTO ?", temp);

                var name = BackupPolicy.FileName(DateTime.Now);
                await _sink.WriteAsync(temp, name);
                LastBackupUtc = DateTime.UtcNow;

                foreach (var old in BackupPolicy.ToPrune(await _sink.ListAsync()))
                {
                    try { await _sink.DeleteAsync(old); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Prune {old} failed: {ex.Message}"); }
                }

                return new BackupResult(true, name, $"Backed up to {Location}/{name}");
            }
            catch (Exception ex)
            {
                return new BackupResult(false, null, $"Backup failed: {ex.Message}");
            }
            finally
            {
                try { File.Delete(temp); } catch { /* cache is cleaned by the OS anyway */ }
                _gate.Release();
            }
        }
    }
}
