using Android.Content;
using Android.Provider;
using Forge.Services.Interfaces;
using Application = Android.App.Application;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Writes backups to the shared <c>Download/Forge</c> folder via MediaStore (API 29+). Shared
    /// storage outlives the app: uninstalling Forge deletes its private data but not these files.
    /// No storage permission is needed to create files; this install can list and delete the ones
    /// it created. Files from a previous install are visible to the user (Files app, or a picker
    /// for restore) but not to this install's queries — which is fine for pruning.
    /// </summary>
    public sealed class DownloadsBackupSink : IBackupSink
    {
        private const string Folder = "Forge";
        private static readonly string Downloads = global::Android.OS.Environment.DirectoryDownloads!;
        private static string RelativePath => $"{Downloads}/{Folder}/";

        public string Location => $"Download/{Folder}";

        private static ContentResolver Resolver => Application.Context.ContentResolver!;

        public async Task WriteAsync(string sourcePath, string fileName)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
                throw new PlatformNotSupportedException("Shared-storage backups need Android 10+.");

            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "application/vnd.sqlite3");
            values.Put(MediaStore.IMediaColumns.RelativePath, RelativePath);
            values.Put(MediaStore.IMediaColumns.IsPending, 1);

            var uri = Resolver.Insert(MediaStore.Downloads.ExternalContentUri!, values)
                      ?? throw new IOException("MediaStore refused to create the backup file.");
            try
            {
                await using (var output = Resolver.OpenOutputStream(uri) ?? throw new IOException("Couldn't open the backup for writing."))
                await using (var input = File.OpenRead(sourcePath))
                {
                    await input.CopyToAsync(output);
                }

                var done = new ContentValues();
                done.Put(MediaStore.IMediaColumns.IsPending, 0);
                Resolver.Update(uri, done, null, null);
            }
            catch
            {
                Resolver.Delete(uri, null, null); // don't leave a half-written backup behind
                throw;
            }
        }

        public Task<IReadOnlyList<string>> ListAsync()
        {
            var names = new List<string>();
            if (!OperatingSystem.IsAndroidVersionAtLeast(29)) return Task.FromResult<IReadOnlyList<string>>(names);

            using var cursor = Resolver.Query(MediaStore.Downloads.ExternalContentUri!,
                new[] { MediaStore.IMediaColumns.DisplayName },
                $"{MediaStore.IMediaColumns.RelativePath} = ?", new[] { RelativePath }, null);
            while (cursor?.MoveToNext() == true)
                if (cursor.GetString(0) is { } name) names.Add(name);
            return Task.FromResult<IReadOnlyList<string>>(names);
        }

        public Task DeleteAsync(string fileName)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                Resolver.Delete(MediaStore.Downloads.ExternalContentUri!,
                    $"{MediaStore.IMediaColumns.RelativePath} = ? AND {MediaStore.IMediaColumns.DisplayName} = ?",
                    new[] { RelativePath, fileName });
            return Task.CompletedTask;
        }
    }
}
