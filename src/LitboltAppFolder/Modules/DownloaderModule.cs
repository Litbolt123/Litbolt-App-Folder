using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Litbolt.Shell;
using LitboltAppFolder.Services;

namespace LitboltAppFolder.Modules;

public sealed class DownloaderModule : IAppModule
{
    private readonly Func<string?> _resolveExe;

    public DownloaderModule(Func<string?> resolveExe) => _resolveExe = resolveExe;

    public string Id => KnownModuleIds.Downloader;
    public string DisplayName => "Downloader";
    public string Description => "YouTube Downloader — queue audio/video downloads.";
    public string? StandaloneExePath => _resolveExe();

    public FrameworkElement CreateView() => new DownloaderPanel(_resolveExe);

    public void DisposeView(FrameworkElement view)
    {
        if (view is DownloaderPanel panel)
            panel.Dispose();
    }
}

internal sealed class DownloaderPanel : UserControl, IDisposable
{
    private readonly Func<string?> _resolveExe;
    private readonly TextBox _urlBox;
    private readonly TextBlock _statusText;
    private readonly TextBlock _connectionText;
    private readonly Button _checkButton;
    private readonly Button _downloadButton;
    private readonly ComboBox _formatBox;
    private readonly ComboBox _qualityBox;
    private readonly DownloaderBridge _api = new();
    private int _port;
    private string _token = "";

    public DownloaderPanel(Func<string?> resolveExe)
    {
        _resolveExe = resolveExe;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1A));

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock
        {
            Text = "YouTube Downloader",
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6),
        });
        root.Children.Add(new TextBlock
        {
            Text = "Queue downloads while YouTube Downloader runs in the background. Pop this tab out or open the full app for history, formats, and browser extension settings.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        });

        _connectionText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
        };
        root.Children.Add(_connectionText);

        root.Children.Add(new TextBlock
        {
            Text = "YouTube URL",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6),
        });
        _urlBox = new TextBox
        {
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x28)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x40)),
            Margin = new Thickness(0, 0, 0, 12),
        };
        root.Children.Add(_urlBox);

        var formatRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        formatRow.Children.Add(new TextBlock
        {
            Text = "Format",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        _formatBox = new ComboBox
        {
            Width = 140,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (var (label, tag) in new[]
                 {
                     ("MP3 (audio)", "mp3"),
                     ("M4A (audio)", "m4a"),
                     ("Opus (audio)", "opus"),
                     ("FLAC (audio)", "flac"),
                     ("WAV (audio)", "wav"),
                     ("MP4 (video)", "mp4"),
                 })
        {
            _formatBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        }
        _formatBox.SelectedIndex = 0;
        _formatBox.SelectionChanged += (_, _) => UpdateQualityOptions();
        formatRow.Children.Add(_formatBox);

        formatRow.Children.Add(new TextBlock
        {
            Text = "Quality",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        _qualityBox = new ComboBox
        {
            Width = 120,
            VerticalAlignment = VerticalAlignment.Center,
        };
        formatRow.Children.Add(_qualityBox);
        root.Children.Add(formatRow);
        UpdateQualityOptions();

        var urlRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var pasteBtn = MakeButton("Paste URL", PasteUrlFromClipboard);
        urlRow.Children.Add(pasteBtn);
        root.Children.Add(urlRow);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _checkButton = MakeAccentButton("Check URL", () => _ = RunCheckAsync());
        _downloadButton = MakeButton("Download", () => _ = RunDownloadAsync(force: false));
        var forceBtn = MakeButton("Force re-download", () => _ = RunDownloadAsync(force: true));
        var openBtn = MakeButton("Open full app", () => DownloaderBridge.LaunchStandalone(_resolveExe()));
        var refreshBtn = MakeButton("Refresh connection", () => _ = RefreshConnectionAsync());
        row.Children.Add(_checkButton);
        row.Children.Add(_downloadButton);
        row.Children.Add(forceBtn);
        row.Children.Add(openBtn);
        row.Children.Add(refreshBtn);
        root.Children.Add(row);

        _statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
            MinHeight = 48,
        };
        root.Children.Add(_statusText);

        Content = root;
        Loaded += async (_, _) => await RefreshConnectionAsync();
    }

    public void Dispose() => _api.Dispose();

    private void UpdateQualityOptions()
    {
        var format = GetSelectedFormatTag();
        var isVideo = string.Equals(format, "mp4", StringComparison.OrdinalIgnoreCase);
        _qualityBox.Items.Clear();
        if (isVideo)
        {
            foreach (var (label, value) in new[] { ("Best", 0), ("1080p", 1080), ("720p", 720), ("480p", 480) })
                _qualityBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }
        else
        {
            foreach (var (label, value) in new[] { ("Best", 0), ("320 kbps", 320), ("256 kbps", 256), ("192 kbps", 192), ("128 kbps", 128) })
                _qualityBox.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        if (_qualityBox.Items.Count > 0)
            _qualityBox.SelectedIndex = 0;
    }

    private string GetSelectedFormatTag()
    {
        if (_formatBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return "mp3";
    }

    private int GetSelectedQuality()
    {
        if (_qualityBox.SelectedItem is ComboBoxItem item && item.Tag is int quality)
            return quality;
        return 0;
    }

    private string GetContentKind(string format) =>
        string.Equals(format, "mp4", StringComparison.OrdinalIgnoreCase) ? "video" : "music";

    private void PasteUrlFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
                _urlBox.Text = Clipboard.GetText().Trim();
        }
        catch
        {
            _statusText.Text = "Could not read clipboard.";
        }
    }

    private async Task RefreshConnectionAsync()
    {
        SetBusy(true);
        try
        {
            var creds = DownloaderBridge.TryLoadCredentials();
            if (creds is null)
            {
                _connectionText.Text =
                    "Extension API not configured. Open YouTube Downloader → Settings and enable the browser extension API.";
                _connectionText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x5A));
                _port = 0;
                _token = "";
                return;
            }

            (_port, _token) = creds.Value;
            var health = await _api.HealthAsync(_port).ConfigureAwait(true);
            if (health.Ok)
            {
                _connectionText.Text = $"Connected to {health.App} v{health.Version} on port {_port}.";
                _connectionText.Foreground = new SolidColorBrush(Color.FromRgb(0x1D, 0xB9, 0x54));
            }
            else
            {
                _connectionText.Text = health.Error ?? "Not connected.";
                _connectionText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x5A));
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunCheckAsync()
    {
        if (!EnsureConnected())
            return;

        var url = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            _statusText.Text = "Paste a YouTube URL first.";
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _api.CheckAsync(_port, _token, url).ConfigureAwait(true);
            _statusText.Text = result.Ok
                ? BuildCheckMessage(result)
                : result.Error ?? "Check failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunDownloadAsync(bool force)
    {
        if (!EnsureConnected())
            return;

        var url = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            _statusText.Text = "Paste a YouTube URL first.";
            return;
        }

        SetBusy(true);
        try
        {
            var format = GetSelectedFormatTag();
            var quality = GetSelectedQuality();
            var result = await _api.DownloadAsync(
                _port, _token, url, force,
                format, quality, GetContentKind(format)).ConfigureAwait(true);
            _statusText.Text = result.Ok
                ? result.Message ?? "Queued."
                : result.Error ?? "Download failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool EnsureConnected()
    {
        if (_port > 0 && !string.IsNullOrWhiteSpace(_token))
            return true;

        _statusText.Text = "Start YouTube Downloader and click Refresh connection.";
        return false;
    }

    private static string BuildCheckMessage(DownloaderCheckResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Title))
            parts.Add(result.Title);
        if (result.AlreadyDownloaded)
            parts.Add("Already on disk.");
        else if (result.InQueue)
            parts.Add("Already in queue.");
        else
            parts.Add("Ready to download.");
        if (!string.IsNullOrWhiteSpace(result.Path))
            parts.Add(result.Path);
        return string.Join(" · ", parts);
    }

    private void SetBusy(bool busy)
    {
        _checkButton.IsEnabled = !busy;
        _downloadButton.IsEnabled = !busy;
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
        btn.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x5A));
        return btn;
    }
}
