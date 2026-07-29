using System.Diagnostics;
using System.Windows;
using Litbolt.Shell.Tray;
using Litbolt.Shell.Updates;

namespace LocalVideoHub;

public partial class SettingsWindow
{
    private readonly VideoHubSettings _settings;
    private string? _pendingInstallerUrl;
    private string? _pendingInstallerVersion;

    public SettingsWindow(VideoHubSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        AutoCheckBox.IsChecked = settings.AutoCheckUpdates;
        MinimizeToTrayBox.IsChecked = settings.MinimizeToTray;
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        StatusText.Text = $"Installed v{GitHubUpdateClient.CurrentAssemblyVersion.ToString(3)}";
        Closed += (_, _) => SaveAndClose();
    }

    private void SaveAndClose()
    {
        _settings.AutoCheckUpdates = AutoCheckBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true || StartWithWindowsBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        VideoHubSettingsStore.Save(_settings);
        AutoStartService.SetEnabled(App.AutoStartRegistryName, _settings.StartWithWindows);
        if (Owner is MainWindow mw)
            mw.OnTrayPreferenceChanged();
    }

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        InstallerPanel.Visibility = Visibility.Collapsed;
        _pendingInstallerUrl = null;
        StatusText.Text = "Checking GitHub Releases…";
        var r = await VideoHubUpdates.Client.CheckLatestReleaseAsync();
        _settings.LastUpdateCheckUtc = DateTime.UtcNow.ToString("o");
        VideoHubSettingsStore.Save(_settings);

        if (r.NoPublishedReleases)
        {
            UpdateAvailabilityCache.Clear();
            StatusText.Text = "No published release yet. Open Releases after the first installer is uploaded.";
            return;
        }

        if (!r.Success)
        {
            StatusText.Text = "Could not check: " + (r.ErrorMessage ?? "unknown");
            return;
        }

        if (r.IsNewerThanCurrent)
        {
            UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
            StatusText.Text = $"Newer release: v{r.LatestVersion} (this app v{GitHubUpdateClient.CurrentAssemblyVersion.ToString(3)}).";
            if (!string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                _pendingInstallerUrl = r.InstallerDownloadUrl;
                _pendingInstallerVersion = r.LatestVersion;
                InstallerPanel.Visibility = Visibility.Visible;
            }

            if (Owner is MainWindow mw)
                mw.RefreshUpdateBanner();
        }
        else
        {
            UpdateAvailabilityCache.Clear();
            StatusText.Text = $"Up to date ({r.LatestVersion}).";
            if (Owner is MainWindow mw)
                mw.RefreshUpdateBanner();
        }
    }

    private void OpenReleases_OnClick(object sender, RoutedEventArgs e) =>
        VideoHubUpdates.Client.OpenReleasesInBrowser();

    private async void DownloadInstaller_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _pendingInstallerUrl;
        var ver = _pendingInstallerVersion;
        if (string.IsNullOrEmpty(url))
        {
            var r = await VideoHubUpdates.Client.CheckLatestReleaseAsync();
            if (!r.Success || string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                MessageBox.Show(this, "No installer available. Open Releases and download manually.",
                    "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            url = r.InstallerDownloadUrl;
            ver = r.LatestVersion;
        }

        StatusText.Text = "Downloading…";
        var (path, err) = await VideoHubUpdates.Client.DownloadInstallerToTempAsync(url!, ver);
        if (path is null)
        {
            StatusText.Text = "Download failed: " + (err ?? "unknown");
            return;
        }

        var confirm = MessageBox.Show(this,
            "The installer will run and Local Video Hub will close so files are not locked.\n\nContinue?",
            "Download and install",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusText.Text = "Installer saved — run it when ready.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            if (Application.Current is App app)
                app.ExitForInstallerUpgrade();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not launch: " + ex.Message;
        }
    }
}
