using Android.App;
using Android.Content;
using Android.Provider;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Opens the system document picker already inside <c>Download/Forge</c>, so restoring a
    /// backup starts among backups rather than on the phone's recent photos and videos. MAUI's
    /// FilePicker can't set a starting folder; ACTION_OPEN_DOCUMENT with EXTRA_INITIAL_URI can.
    /// </summary>
    public static class BackupFilePicker
    {
        internal const int RequestCode = 4301;
        private static TaskCompletionSource<Uri?>? _pending;

        public static async Task<(Stream Stream, string FileName)?> PickAsync()
        {
            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No activity to show the picker.");

            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var forgeFolder = DocumentsContract.BuildDocumentUri(
                    "com.android.externalstorage.documents", "primary:Download/Forge");
                intent.PutExtra(DocumentsContract.ExtraInitialUri, forgeFolder);
            }

            _pending = new TaskCompletionSource<Uri?>();
            activity.StartActivityForResult(intent, RequestCode);
            var uri = await _pending.Task;
            if (uri is null) return null;

            var resolver = Application.Context.ContentResolver!;
            var name = "backup.db3";
            using (var cursor = resolver.Query(uri, new[] { IOpenableColumns.DisplayName }, null, null, null))
                if (cursor?.MoveToFirst() == true && cursor.GetString(0) is { } display)
                    name = display;

            var stream = resolver.OpenInputStream(uri) ?? throw new IOException("Couldn't open that file.");
            return (stream, name);
        }

        /// <summary>Called from <see cref="MainActivity.OnActivityResult"/>.</summary>
        internal static bool HandleResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode != RequestCode) return false;
            _pending?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
            _pending = null;
            return true;
        }
    }
}
