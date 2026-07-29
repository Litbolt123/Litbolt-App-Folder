using System.Diagnostics;
using System.IO;
using System.Windows;
using Litbolt.Shell.Tray;
using Litbolt.Shell.Updates;
using LitboltAppFolder.Services;
using Microsoft.Win32;

namespace LitboltAppFolder;

public partial class SettingsWindow
{
    private readonly FolderSettings _settings;
    private string? _pendingInstallerUrl;
    private string? _pendingInstallerVersion;

    public SettingsWindow(FolderSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        AutoCheckBox.IsChecked = settings.AutoCheckUpdates;
        MinimizeToTrayBox.IsChecked = settings.MinimizeToTray;
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        MusicPathBox.Text = settings.MusicHubExePath ?? "";
        VideoPathBox.Text = settings.VideoHubExePath ?? "";
        DownloaderPathBox.Text = settings.DownloaderExePath ?? "";
        StatusText.Text = string.IsNullOrWhiteSpace(settings.LastUpdateCheckUtc)
            ? $"Installed v{GitHubUpdateClient.CurrentAssemblyVersion.ToString(3)} · never checked"
            : $"Installed v{GitHubUpdateClient.CurrentAssemblyVersion.ToString(3)} · last check {settings.LastUpdateCheckUtc}";
        Closed += (_, _) => SaveAndClose();
    }

    private void SaveAndClose()
    {
        _settings.AutoCheckUpdates = AutoCheckBox.IsChecked == true;
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true || StartWithWindowsBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.MusicHubExePath = NullIfBlank(MusicPathBox.Text);
        _settings.VideoHubExePath = NullIfBlank(VideoPathBox.Text);
        _settings.DownloaderExePath = NullIfBlank(DownloaderPathBox.Text);
        FolderSettingsStore.Save(_settings);

        AutoStartService.SetEnabled(App.AutoStartRegistryName, _settings.StartWithWindows);
        if (Owner is MainWindow mw)
            mw.OnTrayPreferenceChanged();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void BrowseMusic_OnClick(object sender, RoutedEventArgs e) =>
        BrowseExe(MusicPathBox, AppExePaths.DefaultMusicHubExe());

    private void BrowseVideo_OnClick(object sender, RoutedEventArgs e) =>
        BrowseExe(VideoPathBox, AppExePaths.DefaultVideoHubExe());

    private void BrowseDownloader_OnClick(object sender, RoutedEventArgs e) =>
        BrowseExe(DownloaderPathBox, AppExePaths.DefaultDownloaderExe());

    private static void BrowseExe(System.Windows.Controls.TextBox box, string defaultPath)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose application",
            Filter = "Executable (*.exe)|*.exe",
            FileName = Path.GetFileName(defaultPath),
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(defaultPath) ?? "")
                ? Path.GetDirectoryName(defaultPath)
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        if (dlg.ShowDialog() == true)
            box.Text = dlg.FileName;
    }

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        InstallerPanel.Visibility = Visibility.Collapsed;
        _pendingInstallerUrl = null;
        _pendingInstallerVersion = null;
        StatusText.Text = "Checking GitHub Releases…";

        var r = await AppUpdates.Client.CheckLatestReleaseAsync();
        _settings.LastUpdateCheckUtc = DateTime.UtcNow.ToString("o");
        FolderSettingsStore.Save(_settings);

        if (r.NoPublishedReleases)
        {
            UpdateAvailabilityCache.Clear();
            StatusText.Text =
                "No published release on GitHub yet. Use “Open Releases page” after the first installer is uploaded.";
            return;
        }

        if (!r.Success)
        {
            StatusText.Text = "Could not check: " + (r.ErrorMessage ?? "unknown error");
            return;
        }

        var cur = GitHubUpdateClient.CurrentAssemblyVersion.ToString(3);
        if (r.IsNewerThanCurrent)
        {
            UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
            StatusText.Text =
                $"Newer release on GitHub (v{r.LatestVersion}, this app v{cur}). " +
                (string.IsNullOrEmpty(r.InstallerDownloadUrl)
                    ? "No installer attached — open Releases to download manually."
                    : "Use “Download and install…” or open Releases.");

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
            StatusText.Text = $"Up to date with the highest published tag ({r.LatestVersion}).";
            if (Owner is MainWindow mw)
                mw.RefreshUpdateBanner();
        }
    }

    private void OpenReleases_OnClick(object sender, RoutedEventArgs e) =>
        AppUpdates.Client.OpenReleasesInBrowser();

    private async void DownloadInstaller_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _pendingInstallerUrl;
        var ver = _pendingInstallerVersion;
        if (string.IsNullOrEmpty(url))
        {
            StatusText.Text = "Checking for download link…";
            var r = await AppUpdates.Client.CheckLatestReleaseAsync();
            if (!r.Success || !r.IsNewerThanCurrent || string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                MessageBox.Show(this,
                    "No installer download is available. Open the Releases page and download LitboltAppFolder-Setup-….exe manually.",
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            url = r.InstallerDownloadUrl;
            ver = r.LatestVersion;
        }

        StatusText.Text = "Downloading installer…";
        var (path, err) = await AppUpdates.Client.DownloadInstallerToTempAsync(url!, ver);
        if (path is null)
        {
            StatusText.Text = "Download failed: " + (err ?? "unknown");
            return;
        }

        var confirm = MessageBox.Show(this,
            "The installer will run and Litbolt's App Folder will close so files are not locked.\n\nContinue?",
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
            StatusText.Text = "Could not launch installer: " + ex.Message;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true,
                });
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
