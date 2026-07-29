using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Litbolt.Shell;
using Litbolt.Shell.Tray;
using Litbolt.Shell.Updates;
using LitboltAppFolder.Modules;
using LitboltAppFolder.Services;
using LocalVideoHub;

namespace LitboltAppFolder;

public partial class MainWindow : ITrayHost
{
    private readonly List<IAppModule> _modules = [];
    private readonly Dictionary<string, FrameworkElement> _liveViews = new(StringComparer.Ordinal);
    private readonly List<Window> _detachedWindows = [];
    private readonly FolderSettings _settings;
    private readonly AppTrayIcon _tray;
    private readonly ActivateSignalWatcher _activateWatcher;
    private string _currentId = KnownModuleIds.Home;
    private bool _forceClose;
    private bool _updateTrayNotifiedThisSession;

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = TrayIconAssets.CreateWindowIcon(); } catch { /* ignore */ }

        _settings = App.Settings;
        AppTrayIcon? tray = null;
        tray = new AppTrayIcon("Litbolt's App Folder", menu =>
        {
            menu.Items.Add(TrayMenuTheme.CreateHeader("Litbolt's App Folder"));
            menu.Items.Add("Open App Folder", null, (_, _) => tray!.ShowMainWindow());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => tray!.RequestQuit());
        });
        _tray = tray;
        _tray.Attach(this);
        _activateWatcher = new ActivateSignalWatcher(App.DataDirectory, ActivateFromSecondInstance);

        VersionText.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0"}";
        RegisterModules();
        BuildNav();
        ShowModule(KnownModuleIds.Home);
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
        if (version is null || !AppUpdates.ShouldShowBanner(_settings, version))
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
            var r = await AppUpdates.Client.CheckLatestReleaseAsync().ConfigureAwait(true);
            _settings.LastUpdateCheckUtc = DateTime.UtcNow.ToString("o");
            FolderSettingsStore.Save(_settings);

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
        if (!AppUpdates.ShouldShowBanner(_settings, version))
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

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        dlg.ShowDialog();
        RefreshUpdateBanner();
    }

    private void UpdateBannerDownload_OnClick(object sender, RoutedEventArgs e) =>
        AppUpdates.Client.OpenUpdateDownload(UpdateAvailabilityCache.InstallerDownloadUrl);

    private void UpdateBannerLater_OnClick(object sender, RoutedEventArgs e)
    {
        var version = UpdateAvailabilityCache.PendingVersion;
        if (!string.IsNullOrWhiteSpace(version))
            AppUpdates.DismissBanner(_settings, version);
        RefreshUpdateBanner();
    }

    private void RegisterModules()
    {
        _modules.Add(new HomeModule(OpenModule));
        _modules.Add(new MusicHubModule(_settings, () => AppExePaths.ResolveMusicHub(_settings)));
        _modules.Add(new VideoAppModule());
        _modules.Add(new DownloaderModule(() => AppExePaths.ResolveDownloader(_settings)));
    }

    private void BuildNav()
    {
        NavList.Items.Clear();
        foreach (var m in _modules)
            NavList.Items.Add(new NavItem(m.Id, m.DisplayName));
        NavList.SelectedIndex = 0;
    }

    private void NavList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is NavItem item)
            ShowModule(item.Id);
    }

    private void OpenModule(string id)
    {
        var index = _modules.FindIndex(m => m.Id == id);
        if (index >= 0)
            NavList.SelectedIndex = index;
    }

    private void ShowModule(string id)
    {
        _currentId = id;
        var module = _modules.First(m => m.Id == id);
        ContentTitle.Text = module.DisplayName;

        if (!_liveViews.TryGetValue(id, out var view))
        {
            view = module.CreateView();
            _liveViews[id] = view;
        }

        var detached = _detachedWindows.FirstOrDefault(w => w.Content == view);
        if (detached is not null)
        {
            ContentHost.Content = BuildDetachedPlaceholder(module, detached);
            detached.Activate();
            UpdateToolbarState(module);
            return;
        }

        ContentHost.Content = view;
        UpdateToolbarState(module);
    }

    private void UpdateToolbarState(IAppModule module)
    {
        UndockButton.IsEnabled = module.SupportsPopOut && module.Id != KnownModuleIds.Home;
        UndockButton.Visibility = module.SupportsPopOut && module.Id != KnownModuleIds.Home
            ? Visibility.Visible
            : Visibility.Collapsed;

        var exe = ResolveStandaloneExe(module);
        OpenStandaloneButton.IsEnabled = module.Id != KnownModuleIds.Home &&
                                         (!string.IsNullOrWhiteSpace(exe) || module.Id == KnownModuleIds.Video);
        OpenStandaloneButton.Visibility = module.Id == KnownModuleIds.Home
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private string? ResolveStandaloneExe(IAppModule module) =>
        module.Id switch
        {
            KnownModuleIds.Music => AppExePaths.ResolveMusicHub(_settings),
            KnownModuleIds.Downloader => AppExePaths.ResolveDownloader(_settings),
            KnownModuleIds.Video => AppExePaths.ResolveVideoHub(_settings) ?? module.StandaloneExePath,
            _ => module.StandaloneExePath,
        };

    private FrameworkElement BuildDetachedPlaceholder(IAppModule module, Window detached)
    {
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock
        {
            Text = $"{module.DisplayName} is popped out",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This tab’s window is floating. Close that window to bring it back here, or redock below.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20),
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var focus = new Button
        {
            Content = "Focus popped-out window",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x5B, 0x8C, 0xFF)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        focus.Click += (_, _) => detached.Activate();
        var redock = new Button
        {
            Content = "Redock here",
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x48)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        redock.Click += (_, _) => Redock(module.Id, detached);
        row.Children.Add(focus);
        row.Children.Add(redock);
        panel.Children.Add(row);
        return panel;
    }

    private void UndockButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentId == KnownModuleIds.Home)
            return;

        var module = _modules.First(m => m.Id == _currentId);
        if (!module.SupportsPopOut)
            return;

        if (!_liveViews.TryGetValue(_currentId, out var view))
            return;

        if (_detachedWindows.Any(w => w.Content == view))
        {
            _detachedWindows.First(w => w.Content == view).Activate();
            return;
        }

        ContentHost.Content = null;

        var win = new Window
        {
            Title = $"{module.DisplayName} — Litbolt's App Folder",
            Width = 1100,
            Height = 720,
            Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x10, 0x16)),
            Content = view,
            Owner = this,
        };
        try { win.Icon = TrayIconAssets.CreateWindowIcon(); } catch { /* ignore */ }
        win.Closed += (_, _) =>
        {
            _detachedWindows.Remove(win);
            if (!_liveViews.TryGetValue(module.Id, out var v))
                return;
            if (ReferenceEquals(win.Content, v))
                win.Content = null;
            if (_currentId == module.Id)
                ContentHost.Content = v;
        };
        _detachedWindows.Add(win);
        win.Show();
        ContentHost.Content = BuildDetachedPlaceholder(module, win);
    }

    private void Redock(string moduleId, Window detached)
    {
        if (!_liveViews.TryGetValue(moduleId, out var view))
            return;

        detached.Content = null;
        detached.Close();
        _detachedWindows.Remove(detached);
        if (_currentId == moduleId)
            ContentHost.Content = view;
    }

    private void OpenStandaloneButton_OnClick(object sender, RoutedEventArgs e)
    {
        var module = _modules.First(m => m.Id == _currentId);
        var exe = ResolveStandaloneExe(module);

        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            if (module.Id == KnownModuleIds.Video)
            {
                UndockButton_OnClick(sender, e);
                return;
            }

            MessageBox.Show(
                $"Standalone app not found for {module.DisplayName}.\n\nSet the path in Settings → Apps, or install the app.",
                "Litbolt's App Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var win in _detachedWindows.ToList())
        {
            try { win.Close(); } catch { /* ignore */ }
        }

        foreach (var module in _modules)
        {
            if (_liveViews.TryGetValue(module.Id, out var view))
                module.DisposeView(view);
        }

        _tray.Dispose();
        _activateWatcher.Dispose();
        base.OnClosed(e);
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
            _tray.MinimizeToTray("Litbolt's App Folder is still running in the tray.");
            return;
        }
    }

    private sealed record NavItem(string Id, string Title)
    {
        public override string ToString() => Title;
    }
}

internal sealed class HomeModule : IAppModule
{
    private readonly Action<string> _open;

    public HomeModule(Action<string> open) => _open = open;

    public string Id => KnownModuleIds.Home;
    public string DisplayName => "Home";
    public string Description => "Pick an app.";
    public string? StandaloneExePath => null;
    public bool SupportsPopOut => false;

    public FrameworkElement CreateView()
    {
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(32) };
        stack.Children.Add(new TextBlock
        {
            Text = "Your apps",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Each app runs inside the Folder. Pop a tab into its own window, or launch the standalone install.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24),
        });

        var grid = new UniformGrid { Columns = 2 };
        grid.Children.Add(Card("Music", "Playlists & playback control", KnownModuleIds.Music, Color.FromRgb(0x1D, 0xB9, 0x54)));
        grid.Children.Add(Card("Local Video Hub", "Full video player", KnownModuleIds.Video, Color.FromRgb(0x5B, 0x8C, 0xFF)));
        grid.Children.Add(Card("Downloader", "Queue YouTube downloads", KnownModuleIds.Downloader, Color.FromRgb(0xFF, 0x5A, 0x5A)));
        stack.Children.Add(grid);
        root.Content = stack;
        return root;
    }

    private Border Card(string title, string subtitle, string id, Color accent)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x28)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 16, 16),
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(0, 0, 0, 3),
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
        });
        sp.Children.Add(new TextBlock
        {
            Text = subtitle,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
        });
        border.Child = sp;
        border.MouseLeftButtonUp += (_, _) => _open(id);
        return border;
    }

    public void DisposeView(FrameworkElement view) { }
}
