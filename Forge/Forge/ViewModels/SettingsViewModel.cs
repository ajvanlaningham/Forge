using System.Windows.Input;

using Forge.Models;
using Forge.Resources.Strings;
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

        public SettingsViewModel(IUpdateService updates)
        {
            _updates = updates;

            Title = AppResources.SettingsPage_Title;

            InstalledVersion = string.Format(
                AppResources.SettingsPage_InstalledVersion_Format,
                _updates.InstalledVersionName,
                _updates.InstalledVersionCode);

            // AsyncRelayCommand disables itself while running, so the buttons need no
            // IsEnabled binding of their own.
            CheckForUpdatesCommand = new AsyncRelayCommand(CheckAsync, () => !IsDownloading);
            DownloadAndInstallCommand = new AsyncRelayCommand(DownloadAsync, () => UpdateAvailable);
        }

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
        public bool UpdateAvailable { get => _updateAvailable; private set => SetProperty(ref _updateAvailable, value); }

        private bool _isChecking;
        public bool IsChecking { get => _isChecking; private set => SetProperty(ref _isChecking, value); }

        private bool _isDownloading;
        public bool IsDownloading { get => _isDownloading; private set => SetProperty(ref _isDownloading, value); }

        private double _downloadProgress;
        public double DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }

        public ICommand CheckForUpdatesCommand { get; }
        public ICommand DownloadAndInstallCommand { get; }

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
