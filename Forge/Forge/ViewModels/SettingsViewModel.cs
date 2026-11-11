using System.Windows.Input;

using Forge.Constants;
using Forge.Models;
using Forge.Resources.Strings;
using Forge.Services;
using Forge.Services.Interfaces;

namespace Forge.ViewModels
{
    /// <summary>
    /// Settings screen. For now this is the in-app updater and version info; user-tunable
    /// targets land here with Epic 3.
    /// </summary>
    public sealed class SettingsViewModel : BaseViewModel
    {
        private readonly IUpdateService _updates;
        private readonly IBackupService _backup;

        private string _backupStatus = "";
        public string BackupStatus { get => _backupStatus; private set => SetProperty(ref _backupStatus, value); }

        private bool _isBackingUp;
        public bool IsBackingUp { get => _isBackingUp; private set => SetProperty(ref _isBackingUp, value); }

        public ICommand BackUpNowCommand { get; }

        public ICommand RestoreCommand => new Command(async () => await RestoreAsync());

        /// <summary>
        /// PBI 10.2: pick a backup (the system picker can see Download/Forge, including copies from
        /// a previous install), validate it, confirm, restore, then close so the app reopens on it.
        /// </summary>
        private async Task RestoreAsync()
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
                await Shell.Current.DisplayAlert("Restore", $"Couldn't open the file picker: {ex.Message}", "OK");
                return;
            }

            RestoreCheck check;
            await using (stream)
                check = await _backup.InspectAsync(stream, fileName);
            if (!check.Ok)
            {
                await Shell.Current.DisplayAlert("Can't restore that file", check.Message, "OK");
                return;
            }

            var go = await Shell.Current.DisplayAlert("Replace your data?",
                $"{fileName}\n{check.Summary}\n\nEverything currently in Forge is replaced by this backup. " +
                "Your current data is backed up first, so this can be undone by restoring that copy.",
                "Restore", "Cancel");
            if (!go) return;

            var result = await _backup.RestoreAsync(check);
            if (!result.Success)
            {
                await Shell.Current.DisplayAlert("Restore failed", result.Message, "OK");
                return;
            }

            await Shell.Current.DisplayAlert("Restored", result.Message + "\n\nForge will now close. Open it again to continue.", "Close Forge");
            Application.Current?.Quit();
        }

        public ICommand OpenGymEquipmentCommand { get; } = new Command(async () =>
            await Shell.Current.GoToAsync(nameof(Forge.Views.SubPages.GymEquipmentPage)));

        public ICommand RestDownCommand { get; }
        public ICommand RestUpCommand { get; }

        private string _restText = "";
        /// <summary>Default rest between sets when a program doesn't say (PBI 8.3).</summary>
        public string RestText { get => _restText; private set => SetProperty(ref _restText, value); }

        private void ChangeRest(int delta)
        {
            UserSettings.DefaultRestSeconds = Math.Clamp(UserSettings.DefaultRestSeconds + delta, 15, 600);
            var r = UserSettings.DefaultRestSeconds;
            RestText = $"{r / 60}:{r % 60:00}";
        }

        /// <summary>Shows when the last backup ran, so a silently failing backup is noticeable.</summary>
        public void RefreshBackupStatus()
        {
            var last = _backup.LastBackupUtc;
            BackupStatus = last is DateTime t
                ? $"Last backup: {t.ToLocalTime():ddd MMM d, h:mm tt} · {_backup.Location}"
                : $"No backup yet · they go to {_backup.Location}";
        }

        public SettingsViewModel(IUpdateService updates, IBackupService backup)
        {
            _updates = updates;
            _backup = backup;
            BackUpNowCommand = new Command(async () =>
            {
                if (IsBackingUp) return;
                IsBackingUp = true;
                try
                {
                    var result = await _backup.BackUpNowAsync();
                    RefreshBackupStatus();
                    if (!result.Success) BackupStatus = result.Message;
                }
                finally { IsBackingUp = false; }
            });
            RefreshBackupStatus();
            RestDownCommand = new Command(() => ChangeRest(-15));
            RestUpCommand = new Command(() => ChangeRest(+15));
            ChangeRest(0);

            Title = AppResources.SettingsPage_Title;

            InstalledVersion = string.Format(
                AppResources.SettingsPage_InstalledVersion_Format,
                _updates.InstalledVersionName,
                _updates.InstalledVersionCode);

            // These predicates read mutable state, so every property they touch must call
            // RefreshCommands() when it changes — a bound Button caches the last CanExecute
            // it was told and will sit there disabled otherwise.
            _checkCommand = new AsyncRelayCommand(CheckAsync, () => !IsDownloading);
            _downloadCommand = new AsyncRelayCommand(DownloadAsync, () => UpdateAvailable);

            _useKilograms = UserSettings.WeightUnit == WeightUnit.Kilograms;
        }

        private bool _useKilograms;
        /// <summary>
        /// The toggle switches between pounds (off) and kilograms (on). Storage stays in
        /// pounds — see <see cref="Forge.Constants.WeightMath"/> — so flipping this never
        /// touches persisted values, only what the UI shows.
        /// </summary>
        public bool UseKilograms
        {
            get => _useKilograms;
            set
            {
                if (SetProperty(ref _useKilograms, value))
                    UserSettings.WeightUnit = value ? WeightUnit.Kilograms : WeightUnit.Pounds;
            }
        }

        private readonly AsyncRelayCommand _checkCommand;
        private readonly AsyncRelayCommand _downloadCommand;

        public string InstalledVersion { get; }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                    HasStatus = !string.IsNullOrWhiteSpace(value);
            }
        }

        private bool _hasStatus;
        public bool HasStatus { get => _hasStatus; private set => SetProperty(ref _hasStatus, value); }

        private string _updateNotes = string.Empty;
        public string UpdateNotes { get => _updateNotes; private set => SetProperty(ref _updateNotes, value); }

        private bool _hasNotes;
        public bool HasNotes { get => _hasNotes; private set => SetProperty(ref _hasNotes, value); }

        private bool _updateAvailable;
        public bool UpdateAvailable
        {
            get => _updateAvailable;
            private set { if (SetProperty(ref _updateAvailable, value)) RefreshCommands(); }
        }

        private bool _isChecking;
        public bool IsChecking { get => _isChecking; private set => SetProperty(ref _isChecking, value); }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            private set { if (SetProperty(ref _isDownloading, value)) RefreshCommands(); }
        }

        private double _downloadProgress;
        public double DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }

        public ICommand CheckForUpdatesCommand => _checkCommand;
        public ICommand DownloadAndInstallCommand => _downloadCommand;

        private void RefreshCommands()
        {
            _checkCommand.RaiseCanExecuteChanged();
            _downloadCommand.RaiseCanExecuteChanged();
        }

        private async Task CheckAsync()
        {
            if (IsChecking || IsDownloading) return;
            try
            {
                IsChecking = true;
                StatusMessage = AppResources.SettingsPage_Checking;
                UpdateAvailable = false;
                HasNotes = false;

                // CheckAsync never throws; failures arrive as a readable message.
                var result = await _updates.CheckAsync();

                StatusMessage = result.Message;
                UpdateAvailable = result.UpdateAvailable;
                ApplyNotes(result.Latest);
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task DownloadAsync()
        {
            if (IsDownloading) return;

            if (!_updates.CanInstallPackages)
            {
                StatusMessage = AppResources.SettingsPage_NeedsInstallPermission;
                _updates.RequestInstallPermission();
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;

                // Progress<T> marshals callbacks back to the thread it was constructed on.
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    StatusMessage = string.Format(AppResources.SettingsPage_Downloading_Format, p);
                });

                await _updates.DownloadAndInstallAsync(progress);

                // Control passes to the OS installer here; the app stays on this screen.
                StatusMessage = AppResources.SettingsPage_InstallHint;
            }
            catch (Exception)
            {
                // Covers no network, no published release, and a refused install.
                StatusMessage = AppResources.SettingsPage_InstallFailed;
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        private void ApplyNotes(ReleaseManifest? latest)
        {
            if (latest is null || !UpdateAvailable || string.IsNullOrWhiteSpace(latest.Notes))
            {
                UpdateNotes = string.Empty;
                HasNotes = false;
                return;
            }

            UpdateNotes = string.Format(AppResources.SettingsPage_UpdateNotes_Format, latest.Notes);
            HasNotes = true;
        }
    }
}
