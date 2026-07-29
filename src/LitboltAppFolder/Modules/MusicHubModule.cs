using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Litbolt.Shell;
using LitboltAppFolder.Services;

namespace LitboltAppFolder.Modules;

public sealed class MusicHubModule : IAppModule
{
    private readonly FolderSettings _settings;
    private readonly Func<string?> _resolveExe;

    public MusicHubModule(FolderSettings settings, Func<string?> resolveExe)
    {
        _settings = settings;
        _resolveExe = resolveExe;
    }

    public string Id => KnownModuleIds.Music;
    public string DisplayName => "Music";
    public string Description => "Local Music Hub — playlists, playback, and library.";
    public string? StandaloneExePath => _resolveExe();

    public FrameworkElement CreateView() => new MusicHubPanel(_settings, _resolveExe);

    public void DisposeView(FrameworkElement view)
    {
        if (view is MusicHubPanel panel)
            panel.Dispose();
    }
}

internal sealed class MusicHubPanel : UserControl, IDisposable
{
    private readonly Func<string?> _resolveExe;
    private readonly ListBox _playlistList;
    private readonly TextBlock _statusText;
    private readonly TextBlock _nowPlayingText;
    private readonly Slider _volumeSlider;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _nowPlayingTimer;

    public MusicHubPanel(FolderSettings settings, Func<string?> resolveExe)
    {
        _resolveExe = resolveExe;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1A));

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "Local Music Hub",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6),
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = "Browse playlists from your library and control playback. Pop this tab out or open the full app for tagging, imports, and settings.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        };
        Grid.SetRow(subtitle, 1);
        root.Children.Add(subtitle);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        Grid.SetRow(body, 2);

        var left = new DockPanel { Margin = new Thickness(0, 0, 16, 0) };
        var listHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(listHeader, Dock.Top);
        listHeader.Children.Add(new TextBlock
        {
            Text = "Playlists",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var refresh = MakeButton("Refresh", RefreshPlaylists);
        refresh.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(refresh, Dock.Right);
        listHeader.Children.Add(refresh);
        left.Children.Add(listHeader);

        _playlistList = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x28)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            MinHeight = 200,
            DisplayMemberPath = nameof(PlaylistSummary.Display),
        };
        _playlistList.MouseDoubleClick += (_, _) => PlaySelected();
        left.Children.Add(_playlistList);
        Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(new TextBlock
        {
            Text = "Playback",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12),
        });

        var playRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        playRow.Children.Add(MakeAccentButton("Play playlist", PlaySelected));
        playRow.Children.Add(MakeButton("Open full app", () => MusicHubBridge.LaunchStandalone(_resolveExe())));
        right.Children.Add(playRow);

        right.Children.Add(new TextBlock
        {
            Text = "Now playing",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6),
        });
        _nowPlayingText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            FontSize = 12,
            MinHeight = 40,
            Margin = new Thickness(0, 0, 0, 12),
        };
        right.Children.Add(_nowPlayingText);

        right.Children.Add(new TextBlock
        {
            Text = "Volume (sends to running Music Hub)",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            Margin = new Thickness(0, 0, 0, 6),
        });
        _volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0.8,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _volumeSlider.ValueChanged += (_, e) =>
            MusicHubBridge.RequestVolume(e.NewValue, _resolveExe());
        right.Children.Add(_volumeSlider);

        _statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
            FontSize = 12,
        };
        right.Children.Add(_statusText);
        Grid.SetColumn(right, 1);
        body.Children.Add(right);
        root.Children.Add(body);

        var pathHint = new TextBlock
        {
            Margin = new Thickness(0, 16, 0, 0),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x88)),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(pathHint, 3);
        root.Children.Add(pathHint);

        Content = root;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => RefreshPlaylists();
        _refreshTimer.Start();

        _nowPlayingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _nowPlayingTimer.Tick += (_, _) => RefreshNowPlaying();
        _nowPlayingTimer.Start();

        RefreshPlaylists();
        RefreshNowPlaying();
        UpdatePathHint(pathHint);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _nowPlayingTimer.Stop();
    }

    private void RefreshNowPlaying()
    {
        var state = MusicHubBridge.TryReadNowPlaying();
        if (state is null)
        {
            _nowPlayingText.Text = MusicHubBridge.IsMusicHubRunning()
                ? "Nothing playing in Music Hub."
                : "Start Music Hub to see now playing.";
            return;
        }

        var status = state.IsPlaying && !state.IsPaused ? "Playing"
            : state.IsPaused ? "Paused"
            : "Stopped";
        var artistAlbum = string.IsNullOrWhiteSpace(state.Artist)
            ? state.Album
            : string.IsNullOrWhiteSpace(state.Album)
                ? state.Artist
                : $"{state.Artist} — {state.Album}";
        _nowPlayingText.Text = string.IsNullOrWhiteSpace(artistAlbum)
            ? $"{status}: {state.Title}"
            : $"{status}: {state.Title}\n{artistAlbum}";
    }

    private void RefreshPlaylists()
    {
        var playlists = MusicHubBridge.ReadPlaylistSummaries();
        _playlistList.ItemsSource = playlists;

        if (!File.Exists(MusicHubBridge.DatabasePath))
        {
            _statusText.Text =
                "No library database found. Open Local Music Hub once to scan your music, then click Refresh.";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x5A));
            return;
        }

        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0));
        var running = MusicHubBridge.IsMusicHubRunning() ? "Music Hub is running." : "Start Music Hub for playback.";
        _statusText.Text = playlists.Count == 0
            ? $"Library found but no playlists yet. {running}"
            : $"{playlists.Count} playlist(s). Double-click to play. {running}";
    }

    private void PlaySelected()
    {
        if (_playlistList.SelectedItem is not PlaylistSummary summary)
        {
            MessageBox.Show("Select a playlist first.", "Music", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name = summary.Name;

        var exe = _resolveExe();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            MessageBox.Show(
                "Local Music Hub is not installed. Set the path in Settings → Apps or install from Releases.",
                "Music",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MusicHubBridge.RequestPlayPlaylist(name, exe);
        _statusText.Text = $"Sent play request for “{name}”.";
    }

    private void UpdatePathHint(TextBlock block)
    {
        var exe = _resolveExe();
        block.Text = exe is not null
            ? $"EXE: {exe}"
            : "EXE not found — install Local Music Hub or set the path in Settings → Apps.";
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x48)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeAccentButton(string text, Action onClick)
    {
        var btn = MakeButton(text, onClick);
        btn.Background = new SolidColorBrush(Color.FromRgb(0x1D, 0xB9, 0x54));
        return btn;
    }
}
