using System.ComponentModel;
using System.IO;
using System.Windows;
using Litbolt.Shell;
using Litbolt.Shell.Tray;
using Litbolt.Shell.Updates;

namespace LocalVideoHub;

public partial class MainWindow : Window, ITrayHost
{
    private readonly VideoPlayerView _view;
    private readonly VideoHubSettings _settings;
    private readonly AppTrayIcon _tray;
    private readonly ActivateSignalWatcher _activateWatcher;
    private bool _forceClose;
    private bool _updateTrayNotifiedThisSession;

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = TrayIconAssets.CreateWindowIcon(); } catch { /* ignore */ }

        _settings = App.Settings;
        AppTrayIcon? tray = null;
        tray = new AppTrayIcon("Local Video Hub", menu =>
        {
            menu.Items.Add(TrayMenuTheme.CreateHeader("Local Video Hub"));
            menu.Items.Add("Open Local Video Hub", null, (_, _) => tray!.ShowMainWindow());
            menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(OpenSettings));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => tray!.RequestQuit());
        });
        _tray = tray;
        _tray.Attach(this);
        _activateWatcher = new ActivateSignalWatcher(App.DataDirectory, ActivateFromSecondInstance);

        _view = new VideoPlayerView();
        Root.Content = _view;
        Loaded += OnLoaded;
    }

    public bool ShouldMinimizeToTrayOnClose =>
        _settings.MinimizeToTray || _settings.StartWithWindows;

    public void RequestForceClose() => _forceClose = true;

    public void OnTrayPreferenceChanged()
    {
        _tray.Show();
        if (!ShouldMinimizeToTrayOnClose && !IsVisible)
            _tray.ShowMainWindow();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tray.Show();
        _activateWatcher.Start();

        if (System.Windows.Application.Current is App { StartMinimizedToTray: true } ||
            (_settings.StartWithWindows && !IsActive))
        {
            _tray.MinimizeToTray("Running in the system tray. Double-click the icon to open.");
        }

        if (_settings.AutoCheckUpdates)
            _ = RunStartupUpdateCheckAsync();
    }

    public void RefreshUpdateBanner()
    {
        var version = UpdateAvailabilityCache.PendingVersion;
        if (version is null || !VideoHubUpdates.ShouldShowBanner(_settings, version))
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateBannerText.Text = $"Version {version} is on GitHub — download the setup when ready.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private async Task RunStartupUpdateCheckAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12)).ConfigureAwait(true);
            var r = await VideoHubUpdates.Client.CheckLatestReleaseAsync().ConfigureAwait(true);
            _settings.LastUpdateCheckUtc = DateTime.UtcNow.ToString("o");
            VideoHubSettingsStore.Save(_settings);

            if (r.Success && r.IsNewerThanCurrent)
            {
                UpdateAvailabilityCache.Set(r.LatestVersion, r.InstallerDownloadUrl);
                RefreshUpdateBanner();
                NotifyUpdateTrayBalloon(r.LatestVersion);
            }
            else if (r.Success)
            {
                UpdateAvailabilityCache.Clear();
                RefreshUpdateBanner();
            }
        }
        catch
        {
            /* best-effort */
        }
    }

    private void NotifyUpdateTrayBalloon(string? version)
    {
        if (_updateTrayNotifiedThisSession || string.IsNullOrWhiteSpace(version))
            return;
        if (!VideoHubUpdates.ShouldShowBanner(_settings, version))
            return;

        _updateTrayNotifiedThisSession = true;
        _tray.ShowBalloonTip($"Version {version} is available on GitHub. Open the app to download.");
    }

    private void ActivateFromSecondInstance()
    {
        try
        {
            var signal = SingleInstanceService.ActivateSignalPath(App.DataDirectory);
            if (File.Exists(signal))
                File.Delete(signal);
        }
        catch
        {
            /* ignore */
        }

        _tray.ShowMainWindow();
    }

    private void Settings_OnClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        dlg.ShowDialog();
        RefreshUpdateBanner();
        OnTrayPreferenceChanged();
    }

    private void UpdateBannerDownload_OnClick(object sender, RoutedEventArgs e) =>
        VideoHubUpdates.Client.OpenUpdateDownload(UpdateAvailabilityCache.InstallerDownloadUrl);

    private void UpdateBannerLater_OnClick(object sender, RoutedEventArgs e)
    {
        var version = UpdateAvailabilityCache.PendingVersion;
        if (!string.IsNullOrWhiteSpace(version))
            VideoHubUpdates.DismissBanner(_settings, version);
        RefreshUpdateBanner();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { BypassMainWindowCloseCancel: true })
        {
            _forceClose = true;
            e.Cancel = false;
            return;
        }

        if (!_forceClose && ShouldMinimizeToTrayOnClose)
        {
            e.Cancel = true;
            _tray.MinimizeToTray("Local Video Hub is still running in the tray.");
            return;
        }

        _view.Shutdown();
        _tray.Dispose();
        _activateWatcher.Dispose();
    }
}
