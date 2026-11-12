using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using Forge.Constants;
using Forge.Guild;
using Forge.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Forge.Services.Implementations
{
    /// <inheritdoc />
    public sealed class OffsiteBackupService : IOffsiteBackupService
    {
        private const string KeyKey = "offsite.recoveryKey";
        private const string UploadedPref = "Offsite.UploadedUtc", ResultPref = "Offsite.LastResult", SetUpPref = "Offsite.SetUp";

        private readonly IAppDatabase _db;
        private readonly ILogger<OffsiteBackupService> _log;
        private readonly HttpClient _http = new() { BaseAddress = new Uri(GameConstants.Nidavellir.BaseUrl), Timeout = TimeSpan.FromMinutes(2) };
        private readonly SemaphoreSlim _gate = new(1, 1);

        public OffsiteBackupService(IAppDatabase db, ILogger<OffsiteBackupService> log)
        {
            _db = db;
            _log = log;
        }

        public bool IsSetUp => Preferences.Get(SetUpPref, false);

        public DateTime? LastUploadUtc => Preferences.Get(UploadedPref, 0L) is var t and > 0 ? new DateTime(t, DateTimeKind.Utc) : null;

        public string LastResult
        {
            get => Preferences.Get(ResultPref, "");
            private set => Preferences.Set(ResultPref, value);
        }

        public async Task<string> SetUpAsync()
        {
            var key = BackupCrypto.NewRecoveryKey();
            var written = BackupCrypto.FormatKey(key);
            await SecureStorage.Default.SetAsync(KeyKey, written);
            Preferences.Set(SetUpPref, true);
            await UploadNowAsync();
            return written;
        }

        public Task<string?> RecoveryKeyAsync() => SecureStorage.Default.GetAsync(KeyKey);

        private async Task<BackupKeys?> KeysAsync()
            => BackupCrypto.ParseKey(await SecureStorage.Default.GetAsync(KeyKey)) is { } key ? BackupCrypto.Derive(key) : null;

        public async Task<string> UploadNowAsync()
        {
            if (!IsSetUp) return "Off-phone backup isn't set up.";
            await _gate.WaitAsync();
            var temp = Path.Combine(FileSystem.CacheDirectory, $"offsite-{Guid.NewGuid():N}.db3");
            try
            {
                if (await KeysAsync() is not { } keys) return LastResult = "This phone lost its recovery key. Turn off and set up again.";

                // VACUUM INTO writes a complete, consistent copy through SQLite itself.
                await _db.Connection.ExecuteAsync("VACUUM INTO ?", temp);
                var sealedBytes = BackupCrypto.Seal(await File.ReadAllBytesAsync(temp), keys.EncryptionKey);

                using var req = new HttpRequestMessage(HttpMethod.Put, $"v1/backups/{keys.LookupId}") { Content = new ByteArrayContent(sealedBytes) };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keys.WriteToken);
                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return LastResult = $"Nidavellir refused the backup ({(int)resp.StatusCode}).";

                Preferences.Set(UploadedPref, DateTime.UtcNow.Ticks);
                return LastResult = $"Backed up ({sealedBytes.Length / 1024.0:0.#} KB, encrypted).";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Off-phone backup failed");
                return LastResult = "Couldn't reach nidavellir; will try again later.";
            }
            finally
            {
                try { File.Delete(temp); } catch { /* cache; the OS cleans it */ }
                _gate.Release();
            }
        }

        public async Task UploadIfDueAsync()
        {
            if (!IsSetUp) return;
            if (LastUploadUtc is DateTime last && DateTime.UtcNow - last < TimeSpan.FromHours(20)) return;
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            await UploadNowAsync();
        }

        public async Task<string> TurnOffAsync()
        {
            var message = "Off-phone backup is off.";
            if (await KeysAsync() is { } keys)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Delete, $"v1/backups/{keys.LookupId}");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keys.WriteToken);
                    using var resp = await _http.SendAsync(req);
                    message = resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.NotFound
                        ? "Off-phone backup is off and the copy on nidavellir is deleted."
                        : "Off-phone backup is off. The copy on nidavellir couldn't be deleted right now.";
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Off-phone backup delete failed");
                    message = "Off-phone backup is off. Nidavellir wasn't reachable, so its copy is still there.";
                }
            }
            SecureStorage.Default.Remove(KeyKey);
            Preferences.Remove(SetUpPref);
            Preferences.Remove(UploadedPref);
            LastResult = "";
            return message;
        }

        public async Task<(string? Path, string? Error)> DownloadAsync(string recoveryKey)
        {
            if (BackupCrypto.ParseKey(recoveryKey) is not { } key)
                return (null, "That isn't a recovery key. It's 13 groups of 4 letters and digits.");
            var keys = BackupCrypto.Derive(key);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/backups/{keys.LookupId}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", keys.WriteToken);
                using var resp = await _http.SendAsync(req);
                if (resp.StatusCode == HttpStatusCode.NotFound) return (null, "Nidavellir has no backup for that recovery key. Check it for typos.");
                if (!resp.IsSuccessStatusCode) return (null, $"Nidavellir refused ({(int)resp.StatusCode}).");

                var plain = BackupCrypto.Open(await resp.Content.ReadAsByteArrayAsync(), keys.EncryptionKey);
                var path = Path.Combine(FileSystem.CacheDirectory, $"nidavellir-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db3");
                await File.WriteAllBytesAsync(path, plain);

                // Keep backing up under the same key on this phone after the restore.
                await SecureStorage.Default.SetAsync(KeyKey, BackupCrypto.FormatKey(key));
                Preferences.Set(SetUpPref, true);
                return (path, null);
            }
            catch (CryptographicException) { return (null, "The backup couldn't be decrypted with that key."); }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Off-phone restore failed");
                return (null, "Couldn't reach nidavellir. Check the connection and try again.");
            }
        }
    }
}
