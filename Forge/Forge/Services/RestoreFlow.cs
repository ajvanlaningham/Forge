using Forge.Services.Interfaces;

namespace Forge.Services
{
    /// <summary>
    /// PBI 10.2: pick a backup (the system picker can see Download/Forge, including copies from a
    /// previous install), validate it, confirm, restore, then close so the app reopens on it. Shared
    /// by Settings and the first-run screen, so it talks through whichever page is showing.
    /// </summary>
    public static class RestoreFlow
    {
        public static async Task RunAsync(Page page, IBackupService backup)
        {
            Stream? stream;
            string fileName;
            try
            {
#if ANDROID
                // Opens straight into Download/Forge rather than the phone's recent files.
                var picked = await Forge.Platforms.Android.BackupFilePicker.PickAsync();
                if (picked is null) return;
                (stream, fileName) = picked.Value;
#else
                var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a Forge backup" });
                if (picked is null) return;
                stream = await picked.OpenReadAsync();
                fileName = picked.FileName;
#endif
            }
            catch (Exception ex)
            {
                await page.DisplayAlertAsync("Restore", $"Couldn't open the file picker: {ex.Message}", "OK");
                return;
            }

            await RestoreStreamAsync(page, backup, stream, fileName);
        }

        /// <summary>
        /// Restore from nidavellir (Decision 24): ask for the recovery key, download and decrypt,
        /// then the same checks and confirmation as a file from the picker.
        /// </summary>
        public static async Task RunOffsiteAsync(Page page, IBackupService backup, Interfaces.IOffsiteBackupService offsite)
        {
            var key = await page.DisplayPromptAsync("Restore from nidavellir",
                "Enter your recovery key (13 groups of 4, from your password manager).",
                "Restore", "Cancel", placeholder: "XXXX-XXXX-…", maxLength: 80,
                keyboard: Keyboard.Create(KeyboardFlags.CapitalizeCharacter));
            if (string.IsNullOrWhiteSpace(key)) return;

            var (path, error) = await offsite.DownloadAsync(key);
            if (path is null)
            {
                await page.DisplayAlertAsync("Can't restore", error ?? "Something went wrong.", "OK");
                return;
            }
            try { await RestoreStreamAsync(page, backup, File.OpenRead(path), "Backup from nidavellir"); }
            finally { try { File.Delete(path); } catch { /* cache */ } }
        }

        private static async Task RestoreStreamAsync(Page page, IBackupService backup, Stream stream, string fileName)
        {
            RestoreCheck check;
            await using (stream)
                check = await backup.InspectAsync(stream, fileName);
            if (!check.Ok)
            {
                await page.DisplayAlertAsync("Can't restore that file", check.Message, "OK");
                return;
            }

            var go = await page.DisplayAlertAsync("Replace your data?",
                $"{fileName}\n{check.Summary}\n\nEverything currently in Forge is replaced by this backup. " +
                "Your current data is backed up first, so this can be undone by restoring that copy.",
                "Restore", "Cancel");
            if (!go) return;

            var result = await backup.RestoreAsync(check);
            if (!result.Success)
            {
                await page.DisplayAlertAsync("Restore failed", result.Message, "OK");
                return;
            }

            UserSettings.OnboardingDone = true;   // a restored install is an existing user's
            await page.DisplayAlertAsync("Restored", result.Message + "\n\nForge will now close. Open it again to continue.", "Close Forge");
            Application.Current?.Quit();
        }
    }
}
