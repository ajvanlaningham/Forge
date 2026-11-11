using System.Globalization;
using Forge.Constants;
using Forge.Services.Interfaces;
using SQLite;

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

                // Never auto-back-up an empty database. After a reinstall the first launch would
                // otherwise write a blank copy that is the *newest* file — exactly the one a
                // person restoring "the latest backup" would pick. (Manual backups still work.)
                if (await IsEmptyAsync())
                {
                    System.Diagnostics.Debug.WriteLine("Scheduled backup skipped: nothing logged yet.");
                    return;
                }

                var result = await BackUpNowAsync();
                System.Diagnostics.Debug.WriteLine($"Scheduled backup: {result.Message}");
            }
            catch (Exception ex)
            {
                // Never block or interrupt the app over a backup. Tomorrow's launch retries.
                System.Diagnostics.Debug.WriteLine($"Scheduled backup failed: {ex}");
            }
        }

        /// <summary>True when nothing has ever been logged: no sessions, weigh-ins, or completed quests.</summary>
        private async Task<bool> IsEmptyAsync()
        {
            var c = _db.Connection;
            async Task<int> Count(string sql)
            {
                try { return await c.ExecuteScalarAsync<int>(sql); }
                catch (SQLiteException) { return 0; } // table not created yet
            }
            var total = await Count("SELECT COUNT(*) FROM TrainingSession WHERE Status = 1")
                        + await Count("SELECT COUNT(*) FROM WeightEntry")
                        + await Count("SELECT COUNT(*) FROM QuestCompletion WHERE Completed = 1")
                        + await Count("SELECT COUNT(*) FROM ProgramDayCompletion WHERE Completed = 1");
            return total == 0;
        }

        public async Task<RestoreCheck> InspectAsync(Stream picked, string fileName)
        {
            var staged = Path.Combine(FileSystem.CacheDirectory, $"restore-{Guid.NewGuid():N}.db3");
            await using (var output = File.Create(staged))
                await picked.CopyToAsync(output);

            RestoreCheck Fail(string why)
            {
                try { File.Delete(staged); } catch { /* best effort */ }
                return new RestoreCheck(false, why, null, 0, "");
            }

            // 1. Is it SQLite at all?
            var header = new byte[16];
            await using (var f = File.OpenRead(staged))
                if (await f.ReadAsync(header) < 16 || System.Text.Encoding.ASCII.GetString(header, 0, 15) != "SQLite format 3")
                    return Fail("That file isn't a Forge backup (not a database).");

            // 2. Is it Forge's, and which schema? The name carries it; a renamed file is inferred.
            try
            {
                using var conn = new SQLiteConnection(staged, SQLiteOpenFlags.ReadOnly);
                var tables = conn.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'table'").ToHashSet();
                if (!tables.Contains("UserStats"))
                    return Fail("That database isn't from Forge.");
                if (conn.ExecuteScalar<string>("PRAGMA integrity_check") != "ok")
                    return Fail("That backup is damaged (integrity check failed).");

                var schema = BackupPolicy.SchemaOf(fileName) ?? (tables.Contains("TrainingSession") ? 2 : 1);
                if (schema > BackupPolicy.SchemaVersion)
                    return Fail($"That backup was made by a newer version of Forge (schema {schema}). Update Forge first.");

                int Count(string table, string where = "") =>
                    tables.Contains(table) ? conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM \"{table}\" {where}") : 0;
                var sessions = Count("TrainingSession", "WHERE Status = 1"); // finished only
                var weighIns = Count("WeightEntry");
                var xp = Count("UserStats") == 0 ? 0 : conn.ExecuteScalar<int>("SELECT Xp FROM UserStats LIMIT 1");
                var summary = $"{sessions} session{(sessions == 1 ? "" : "s")} · {weighIns} weigh-in{(weighIns == 1 ? "" : "s")} · {xp} XP";

                // Older schemas restore as-is: missing tables and columns are created on first use.
                return new RestoreCheck(true, "Looks good.", staged, schema, summary);
            }
            catch (Exception ex)
            {
                return Fail($"Couldn't read that backup: {ex.Message}");
            }
        }

        public async Task<BackupResult> RestoreAsync(RestoreCheck check)
        {
            if (!check.Ok || check.StagedPath is null || !File.Exists(check.StagedPath))
                return new BackupResult(false, null, "Nothing to restore.");

            // Safety net: whatever is live right now gets backed up before it is replaced.
            var safety = await BackUpNowAsync();
            if (!safety.Success)
                return new BackupResult(false, null, $"Restore cancelled: couldn't back up the current data first ({safety.Message}).");

            await _db.CloseAsync();
            foreach (var side in new[] { "-journal", "-wal", "-shm" })
                try { File.Delete(_db.Path + side); } catch { /* not present */ }
            File.Copy(check.StagedPath, _db.Path, overwrite: true);
            try { File.Delete(check.StagedPath); } catch { /* cache */ }

            // Preferences that cache facts about the database: let them be re-derived.
            Preferences.Remove("Progress.LastCelebratedLevel");      // re-seeded silently from the restored XP
            Preferences.Remove(GameConstants.Exercises.PrefixKey);   // library re-upserts into the restored DB

            return new BackupResult(true, null, $"Restored ({check.Summary}). The current data was saved as {safety.FileName}.");
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
