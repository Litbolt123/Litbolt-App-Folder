using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Litbolt.Shell;
using Microsoft.Win32;

namespace LocalVideoHub;

public sealed class VideoAppModule : IAppModule
{
    public string Id => KnownModuleIds.Video;
    public string DisplayName => "Local Video Hub";
    public string Description => "Local video library and player.";
    public string? StandaloneExePath =>
        AppPathResolver.FindInstalledExe("LocalVideoHub", "LocalVideoHub.exe")
        ?? AppPathResolver.FindInstalledExe("LitboltVideo", "LitboltVideo.exe");

    public FrameworkElement CreateView() => new VideoPlayerView();

    public void DisposeView(FrameworkElement view)
    {
        if (view is VideoPlayerView v)
            v.Shutdown();
    }
}

public sealed class VideoPlayerView : UserControl
{
    private static readonly string[] Extensions =
        [".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".m4v", ".flv", ".mpeg", ".mpg", ".m2ts", ".ts"];

    private readonly ListBox _list;
    private readonly HybridVideoHost _video;
    private readonly TextBlock _nowPlaying;
    private readonly TextBlock _folderLabel;
    private readonly TextBlock _elapsedText;
    private readonly TextBlock _remainingText;
    private readonly Slider _seek;
    private readonly Slider _volume;
    private readonly Button _playPauseButton;
    private readonly Button _rotateButton;
    private readonly System.Windows.Shapes.Path _playIcon;
    private readonly System.Windows.Shapes.Path _pauseIcon;
    private readonly ComboBox _speedBox;
    private readonly ComboBox? _audioTrackBox;
    private readonly ComboBox? _subtitleTrackBox;
    private bool _suppressTrackPicker;
    private readonly CheckBox _recursiveBox;
    private readonly TextBox _filterBox;
    private readonly DispatcherTimer _timer;
    private readonly ObservableCollection<VideoItem> _visibleItems = [];
    private readonly List<VideoItem> _allItems = [];
    private readonly List<VideoItem> _queue = [];
    private readonly VideoSettingsData _settings;

    private string? _folder;
    private bool _scrubbing;
    private bool _seekPending;
    private bool _seekCommitted;
    private bool _suppressSeekSlider;
    private bool _isPaused = true;
    private bool _muted;
    private double _volumeBeforeMute = 0.8;
    private int _queueIndex = -1;
    private VideoRepeatMode _repeatMode;
    private string? _currentFilePath;
    private bool _fullscreen;
    private Border? _videoStage;
    private VideoFullscreenWindow? _fullscreenWindow;

    public VideoPlayerView()
    {
        Focusable = true;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x1A));
        _settings = VideoSettings.Load();
        _repeatMode = VideoSettings.ParseRepeat(_settings.RepeatMode);

        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new DockPanel();
        Grid.SetColumn(left, 0);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = "Library",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
        });

        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var openFile = MakeTextButton("Open file…", () => OpenFile());
        openFile.Margin = new Thickness(0, 0, 8, 0);
        var pickFolder = MakeTextButton("Folder…", PickFolder);
        topRow.Children.Add(openFile);
        topRow.Children.Add(pickFolder);
        header.Children.Add(topRow);

        _recursiveBox = new CheckBox
        {
            Content = "Include subfolders",
            Foreground = Brushes.White,
            IsChecked = _settings.RecursiveScan,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _recursiveBox.Checked += (_, _) => { _settings.RecursiveScan = true; SaveSettings(); ReloadFolder(); };
        _recursiveBox.Unchecked += (_, _) => { _settings.RecursiveScan = false; SaveSettings(); ReloadFolder(); };
        header.Children.Add(_recursiveBox);

        _filterBox = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x28)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x40)),
        };
        _filterBox.TextChanged += (_, _) => ApplyFilter();
        var filterHint = new TextBlock
        {
            Text = "Filter",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
            Margin = new Thickness(0, 0, 0, 4),
        };
        header.Children.Add(filterHint);
        header.Children.Add(_filterBox);

        _folderLabel = new TextBlock
        {
            Text = "No folder selected",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0xA0)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };
        header.Children.Add(_folderLabel);
        left.Children.Add(header);

        _list = new ListBox
        {
            ItemsSource = _visibleItems,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1E, 0x28)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
        };
        _list.DisplayMemberPath = nameof(VideoItem.Name);
        _list.MouseDoubleClick += List_OnDoubleClick;
        left.Children.Add(_list);
        root.Children.Add(left);

        var right = new DockPanel();
        Grid.SetColumn(right, 2);

        _nowPlaying = new TextBlock
        {
            Text = "Nothing playing",
            FontSize = 14,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        DockPanel.SetDock(_nowPlaying, Dock.Top);
        right.Children.Add(_nowPlaying);

        _video = new HybridVideoHost
        {
            Volume = Math.Clamp(_settings.Volume, 0, 1),
            SpeedRatio = Math.Clamp(_settings.Speed, 0.25, 4),
        };
        _video.MediaOpened += Media_OnOpened;
        _video.MediaEnded += Media_OnEnded;
        _video.MediaFailed += (_, ex) => Media_OnFailed(_, ex);
        _video.VideoMouseLeftButtonDown += Media_OnMouseLeftButtonDown;

        var transport = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(transport, Dock.Bottom);

        var seekRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        seekRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        seekRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        seekRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

        _elapsedText = VideoTransportUi.CreateTimeLabel();
        _elapsedText.HorizontalAlignment = HorizontalAlignment.Right;
        _elapsedText.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(_elapsedText, 0);
        seekRow.Children.Add(_elapsedText);

        _seek = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            IsMoveToPointEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_seek, 1);
        seekRow.Children.Add(_seek);

        _remainingText = VideoTransportUi.CreateTimeLabel();
        _remainingText.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_remainingText, 2);
        seekRow.Children.Add(_remainingText);
        transport.Children.Add(seekRow);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4),
        };
        controls.Children.Add(VideoTransportUi.CreatePrevButton(PlayPrevious));
        (_playPauseButton, _playIcon, _pauseIcon) = VideoTransportUi.CreatePlayPauseButton(TogglePlayPause);
        controls.Children.Add(_playPauseButton);
        controls.Children.Add(VideoTransportUi.CreateNextButton(() => PlayNext(manual: true)));
        controls.Children.Add(VideoTransportUi.CreateStopButton(StopPlayback));
        controls.Children.Add(VideoTransportUi.CreateRepeatButton(CycleRepeatMode));
        controls.Children.Add(MakeTextButton("−10s", () => SkipSeconds(-10)));
        controls.Children.Add(MakeTextButton("+10s", () => SkipSeconds(10)));
        controls.Children.Add(MakeTextButton("Fullscreen", ToggleFullscreen));
        _rotateButton = MakeTextButton("Rotate", CycleVideoRotation);
        controls.Children.Add(_rotateButton);
        controls.Children.Add(MakeTextButton("Subtitles…", PickSubtitleFile));
        transport.Children.Add(controls);

        var trackRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
        };
        if (_video.LibVlcAvailable)
        {
            trackRow.Children.Add(new TextBlock
            {
                Text = "Audio",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });
            _audioTrackBox = new ComboBox
            {
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                IsEnabled = false,
            };
            _audioTrackBox.SelectionChanged += (_, _) =>
            {
                if (_suppressTrackPicker || _audioTrackBox.SelectedItem is not TrackPickerItem item)
                    return;

                _video.SetAudioTrack(item.Id);
            };
            trackRow.Children.Add(_audioTrackBox);

            trackRow.Children.Add(new TextBlock
            {
                Text = "Subs",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });
            _subtitleTrackBox = new ComboBox
            {
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = false,
            };
            _subtitleTrackBox.SelectionChanged += (_, _) =>
            {
                if (_suppressTrackPicker || _subtitleTrackBox.SelectedItem is not TrackPickerItem item)
                    return;

                _video.SetSubtitleTrack(item.Id);
            };
            trackRow.Children.Add(_subtitleTrackBox);
            transport.Children.Add(trackRow);
        }

        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        _volume = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Width = 100,
            Value = _video.Volume,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        _volume.ValueChanged += (_, e) =>
        {
            if (_muted && e.NewValue > 0)
                _muted = false;
            _video.Volume = e.NewValue;
            _settings.Volume = e.NewValue;
            SaveSettings();
        };
        options.Children.Add(new TextBlock
        {
            Text = "Vol",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });
        options.Children.Add(_volume);
        options.Children.Add(MakeTextButton("Mute", ToggleMute));

        options.Children.Add(new TextBlock
        {
            Text = "Speed",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 6, 0),
        });
        _speedBox = new ComboBox
        {
            Width = 72,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        foreach (var speed in new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 })
            _speedBox.Items.Add(speed);
        _speedBox.SelectedItem = _speedBox.Items.Cast<double>()
            .OrderBy(s => Math.Abs(s - _settings.Speed))
            .First();
        _speedBox.SelectionChanged += (_, _) =>
        {
            if (_speedBox.SelectedItem is double speed)
            {
                _video.SpeedRatio = speed;
                _settings.Speed = speed;
                SaveSettings();
            }
        };
        options.Children.Add(_speedBox);
        transport.Children.Add(options);

        var hint = new TextBlock
        {
            Text = "Space play/pause · ←/→ seek · ↑/↓ volume · F fullscreen · R rotate · O open file",
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x88)),
            FontSize = 11,
            Margin = new Thickness(0, 6, 0, 0),
        };
        transport.Children.Add(hint);
        right.Children.Add(transport);

        _videoStage = new Border { Background = Brushes.Black, Child = _video };
        RenderOptions.SetBitmapScalingMode(_videoStage, BitmapScalingMode.HighQuality);
        right.Children.Add(_videoStage);
        root.Children.Add(right);

        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateSeekUi();
        _timer.Start();

        AllowDrop = true;
        DragOver += OnDragOver;
        Drop += OnDrop;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            HookSeekSlider();
            Keyboard.Focus(this);
            UpdatePlayPauseIcon();
        };

        if (!string.IsNullOrWhiteSpace(_settings.LibraryFolder) && Directory.Exists(_settings.LibraryFolder))
            LoadFolder(_settings.LibraryFolder);

        LoadRecentIntoList();
    }

    public void Shutdown()
    {
        ExitVideoFullscreen();
        _timer.Stop();
        _video.Shutdown();
    }

    private void CycleVideoRotation()
    {
        _video.CycleRotation();
        if (!string.IsNullOrWhiteSpace(_currentFilePath))
        {
            VideoSettings.SetFileRotation(_settings, _currentFilePath, _video.RotationDegrees);
            SaveSettings();
        }

        UpdateRotateButtonLabel();
    }

    private void UpdateRotateButtonLabel()
    {
        _rotateButton.Content = _video.RotationDegrees == 0
            ? "Rotate"
            : $"Rotate {_video.RotationDegrees}°";
    }

    private void PickSubtitleFile()
    {
        if (!_video.LibVlcAvailable)
        {
            MessageBox.Show(
                "Subtitles require the LibVLC engine, which is not available on this PC.",
                "Local Video Hub",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!_video.IsLibVlcActive)
        {
            _video.TryFallbackToLibVlc();
            if (!_video.IsLibVlcActive)
            {
                MessageBox.Show(
                    "Could not switch this file to the LibVLC engine for subtitles.",
                    "Local Video Hub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var dlg = new OpenFileDialog
        {
            Title = "Load subtitle file",
            Filter = "Subtitles|*.srt;*.vtt;*.ass|All files|*.*",
        };
        if (dlg.ShowDialog() != true)
            return;

        _video.LoadSubtitleFile(dlg.FileName);
        Dispatcher.BeginInvoke(RefreshTrackPickers, DispatcherPriority.Background);
    }

    private void SaveSettings() => VideoSettings.Save(_settings);

    private void PickFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choose a video folder" };
        if (dlg.ShowDialog() != true)
            return;
        LoadFolder(dlg.FolderName);
    }

    private void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open video file",
            Filter = "Video files|*.mp4;*.mkv;*.webm;*.avi;*.mov;*.wmv;*.m4v|All files|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true)
            return;

        var items = dlg.FileNames
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new VideoItem(f))
            .ToList();
        if (items.Count == 0)
            return;

        SetQueue(items, 0);
        PlayItem(items[0]);
    }

    private void LoadFolder(string folder)
    {
        _folder = folder;
        _folderLabel.Text = folder;
        _settings.LibraryFolder = folder;
        SaveSettings();
        ReloadFolder();
    }

    private void ReloadFolder()
    {
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
            return;

        _allItems.Clear();
        try
        {
            var option = _settings.RecursiveScan ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(_folder, "*.*", option)
                         .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                _allItems.Add(new VideoItem(file));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read folder:\n{ex.Message}", "Local Video Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        ApplyFilter();
    }

    private void LoadRecentIntoList()
    {
        foreach (var path in _settings.RecentFiles.Where(File.Exists).Take(8))
            _allItems.Add(new VideoItem(path));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = _filterBox.Text?.Trim() ?? "";
        _visibleItems.Clear();
        foreach (var item in _allItems)
        {
            if (string.IsNullOrEmpty(filter) ||
                item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _visibleItems.Add(item);
            }
        }
    }

    private void List_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_list.SelectedItem is not VideoItem item)
            return;

        var index = _visibleItems.IndexOf(item);
        SetQueue(_visibleItems.ToList(), index);
        PlayItem(item);
    }

    private void SetQueue(IReadOnlyList<VideoItem> items, int startIndex)
    {
        _queue.Clear();
        _queue.AddRange(items);
        _queueIndex = startIndex;
    }

    private void PlayItem(VideoItem item)
    {
        try
        {
            _currentFilePath = item.Path;
            var rotation = VideoSettings.ResolveRotation(_settings, item.Path);
            _video.Open(item.Path, rotation);
            _isPaused = false;
            UpdatePlayPauseIcon();
            UpdateRotateButtonLabel();
            _nowPlaying.Text = item.Name;
            RememberRecent(item.Path);
            _queueIndex = _queue.FindIndex(q => string.Equals(q.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not play:\n{ex.Message}", "Local Video Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RememberRecent(string path)
    {
        _settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _settings.RecentFiles.Insert(0, path);
        if (_settings.RecentFiles.Count > 20)
            _settings.RecentFiles = _settings.RecentFiles.Take(20).ToList();
        SaveSettings();
    }

    private void TogglePlayPause()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            if (_list.SelectedItem is VideoItem item)
                List_OnDoubleClick(_list, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
            return;
        }

        if (_isPaused)
        {
            _video.Play();
            _isPaused = false;
            UpdatePlayPauseIcon();
        }
        else
        {
            _video.Pause();
            _isPaused = true;
            UpdatePlayPauseIcon();
        }
    }

    private void StopPlayback()
    {
        _video.Stop();
        _currentFilePath = null;
        _isPaused = true;
        UpdatePlayPauseIcon();
        _nowPlaying.Text = "Nothing playing";
        SetSeekSliderFraction(0);
        SetTimeLabels(TimeSpan.Zero, TimeSpan.Zero);
    }

    private void PlayPrevious()
    {
        if (_queue.Count == 0)
            return;

        if (_video.Position.TotalSeconds > 3)
        {
            SeekTo(TimeSpan.Zero);
            return;
        }

        var index = _queueIndex <= 0 ? _queue.Count - 1 : _queueIndex - 1;
        SetQueue(_queue, index);
        PlayItem(_queue[index]);
    }

    private void PlayNext(bool manual)
    {
        if (_queue.Count == 0)
            return;

        var index = _queueIndex < 0 ? 0 : (_queueIndex + 1) % _queue.Count;
        if (!manual && _repeatMode == VideoRepeatMode.Off && _queueIndex >= 0 && _queueIndex + 1 >= _queue.Count)
            return;

        SetQueue(_queue, index);
        PlayItem(_queue[index]);
    }

    private void SkipSeconds(int delta)
    {
        if (!_video.HasDuration)
            return;

        var target = _video.Position + TimeSpan.FromSeconds(delta);
        if (target < TimeSpan.Zero)
            target = TimeSpan.Zero;
        if (target > _video.Duration)
            target = _video.Duration;
        SeekTo(target);
    }

    private void CycleRepeatMode()
    {
        _repeatMode = _repeatMode switch
        {
            VideoRepeatMode.Off => VideoRepeatMode.All,
            VideoRepeatMode.All => VideoRepeatMode.One,
            _ => VideoRepeatMode.Off,
        };
        _settings.RepeatMode = VideoSettings.RepeatToString(_repeatMode);
        SaveSettings();
    }

    private void UpdatePlayPauseIcon()
    {
        _playIcon.Visibility = _isPaused ? Visibility.Visible : Visibility.Collapsed;
        _pauseIcon.Visibility = _isPaused ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HookSeekSlider()
    {
        _seek.ApplyTemplate();

        _seek.AddHandler(
            UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler((_, _) => EndSeekScrub()),
            handledEventsToo: true);
        _seek.PreviewMouseLeftButtonDown += (_, _) => BeginSeekScrub();
        _seek.LostMouseCapture += (_, _) => EndSeekScrub();
        _seek.ValueChanged += SeekSlider_OnValueChanged;

        if (FindVisualChild<Thumb>(_seek) is Thumb thumb)
        {
            thumb.DragStarted += (_, _) => BeginSeekScrub();
            thumb.DragCompleted += (_, _) => EndSeekScrub();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void BeginSeekScrub()
    {
        if (_scrubbing)
            return;

        _scrubbing = true;
        _seekCommitted = false;
    }

    private void EndSeekScrub()
    {
        if (!_scrubbing || _seekPending)
            return;

        _seekPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _seekPending = false;
            if (_scrubbing)
                CommitSeekScrub();
            _scrubbing = false;
        }, DispatcherPriority.Input);
    }

    private void SeekSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSeekSlider)
            return;

        if (!_scrubbing && Mouse.LeftButton == MouseButtonState.Pressed)
            BeginSeekScrub();

        if (!_scrubbing)
            return;

        UpdateTimeLabelsFromSlider();
    }

    private void CommitSeekScrub()
    {
        if (_seekCommitted || !_video.HasDuration)
            return;

        var totalSeconds = _video.Duration.TotalSeconds;
        if (totalSeconds <= 0)
            return;

        _seekCommitted = true;
        var fraction = Math.Clamp(_seek.Value, 0, 1);
        var target = TimeSpan.FromSeconds(fraction * totalSeconds);
        SeekTo(target);
        SetSeekSliderFraction(fraction);
        SetTimeLabels(target, _video.Duration);
    }

    private void UpdateTimeLabelsFromSlider()
    {
        if (!_video.HasDuration)
            return;

        var duration = _video.Duration;
        if (duration.TotalSeconds <= 0)
            return;

        var preview = TimeSpan.FromSeconds(Math.Clamp(_seek.Value, 0, 1) * duration.TotalSeconds);
        SetTimeLabels(preview, duration);
    }

    private void SetSeekSliderFraction(double fraction)
    {
        _suppressSeekSlider = true;
        _seek.Value = Math.Clamp(fraction, 0, 1);
        _suppressSeekSlider = false;
    }

    private void SeekTo(TimeSpan target)
    {
        if (!_video.HasDuration || string.IsNullOrWhiteSpace(_currentFilePath))
            return;

        if (target < TimeSpan.Zero)
            target = TimeSpan.Zero;
        if (target > _video.Duration)
            target = _video.Duration;

        try
        {
            var wasPlaying = !_isPaused;
            if (wasPlaying)
                _video.Pause();

            _video.Position = target;

            if (_video.HasDuration)
                SetTimeLabels(target, _video.Duration);

            if (wasPlaying)
            {
                _video.Play();
                _isPaused = false;
            }
        }
        catch
        {
            /* seek can fail on some codecs or during open/buffer */
        }
    }

    private void ToggleMute()
    {
        if (_muted)
        {
            _muted = false;
            _volume.Value = _volumeBeforeMute;
        }
        else
        {
            _volumeBeforeMute = _volume.Value;
            _muted = true;
            _volume.Value = 0;
        }
    }

    private void ToggleFullscreen()
    {
        if (_fullscreen)
            ExitVideoFullscreen();
        else
            EnterVideoFullscreen();
    }

    private void EnterVideoFullscreen()
    {
        if (_fullscreen || _videoStage is null)
            return;

        _videoStage.Child = null;
        var surface = _video.DetachActiveVisual();
        _fullscreenWindow = new VideoFullscreenWindow(surface);
        _fullscreenWindow.ExitRequested += ExitVideoFullscreen;
        _fullscreenWindow.Closed += (_, _) =>
        {
            if (_fullscreen)
                ExitVideoFullscreen();
        };
        _fullscreenWindow.Show();
        _fullscreen = true;
    }

    private void ExitVideoFullscreen()
    {
        if (!_fullscreen)
            return;

        _fullscreen = false;

        if (_fullscreenWindow is not null)
        {
            _fullscreenWindow.ExitRequested -= ExitVideoFullscreen;
            _fullscreenWindow.Content = null;
            try { _fullscreenWindow.Close(); } catch { /* ignore */ }
            _fullscreenWindow = null;
        }

        if (_videoStage is not null)
        {
            _video.AttachActiveVisual();
            if (!ReferenceEquals(_videoStage.Child, _video))
                _videoStage.Child = _video;
        }
    }

    private void Media_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            ToggleFullscreen();
    }

    private void Media_OnOpened(object? sender, RoutedEventArgs e)
    {
        SetSeekSliderFraction(0);
        UpdateSeekUi();
        UpdateRotateButtonLabel();
        Dispatcher.BeginInvoke(RefreshTrackPickers, DispatcherPriority.Background);
    }

    private void RefreshTrackPickers()
    {
        if (_audioTrackBox is null && _subtitleTrackBox is null)
            return;

        _suppressTrackPicker = true;
        try
        {
            if (_audioTrackBox is not null)
            {
                var audioTracks = _video.GetAudioTracks();
                _audioTrackBox.Items.Clear();
                foreach (var track in audioTracks)
                    _audioTrackBox.Items.Add(new TrackPickerItem(track.Id, track.Name));

                _audioTrackBox.IsEnabled = audioTracks.Count > 1;
                if (audioTracks.Count > 0)
                {
                    var current = _video.CurrentAudioTrackId;
                    var idx = audioTracks.ToList().FindIndex(t => t.Id == current);
                    _audioTrackBox.SelectedIndex = idx >= 0 ? idx : 0;
                }
            }

            if (_subtitleTrackBox is not null)
            {
                var subtitleTracks = _video.GetSubtitleTracks();
                _subtitleTrackBox.Items.Clear();
                _subtitleTrackBox.Items.Add(new TrackPickerItem(-1, "Off"));
                foreach (var track in subtitleTracks)
                    _subtitleTrackBox.Items.Add(new TrackPickerItem(track.Id, track.Name));

                _subtitleTrackBox.IsEnabled = subtitleTracks.Count > 0;
                if (subtitleTracks.Count > 0)
                {
                    var current = _video.CurrentSubtitleTrackId;
                    var match = _subtitleTrackBox.Items.Cast<TrackPickerItem>()
                        .FirstOrDefault(t => t.Id == current);
                    _subtitleTrackBox.SelectedItem = match ?? _subtitleTrackBox.Items[0];
                }
                else
                    _subtitleTrackBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressTrackPicker = false;
        }
    }

    private void Media_OnEnded(object? sender, RoutedEventArgs e)
    {
        if (_repeatMode == VideoRepeatMode.One)
        {
            SeekTo(TimeSpan.Zero);
            _video.Play();
            return;
        }

        if (_repeatMode == VideoRepeatMode.All || _queueIndex + 1 < _queue.Count)
            PlayNext(manual: false);
        else
        {
            _isPaused = true;
            UpdatePlayPauseIcon();
        }
    }

    private void Media_OnFailed(object? sender, Exception? error)
    {
        if (_video.TryFallbackToLibVlc())
            return;

        MessageBox.Show(
            $"Playback failed.\n\n{error?.Message ?? "Unsupported codec or file."}\n\nTrying next…",
            "Local Video Hub",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        PlayNext(manual: false);
    }

    private void UpdateSeekUi()
    {
        if (_scrubbing)
            return;

        if (_video.HasDuration && _video.Duration.TotalSeconds > 0)
        {
            var ratio = _video.Position.TotalSeconds / _video.Duration.TotalSeconds;
            if (!double.IsNaN(ratio) && !double.IsInfinity(ratio))
                SetSeekSliderFraction(ratio);
            SetTimeLabels(_video.Position, _video.Duration);
        }
    }

    private void SetTimeLabels(TimeSpan pos, TimeSpan total)
    {
        _elapsedText.Text = FormatTime(pos);
        _remainingText.Text = FormatTime(total);
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}";
        return $"{t.Minutes}:{t.Seconds:D2}";
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0)
            return;

        var items = new List<VideoItem>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                LoadFolder(path);
                return;
            }

            if (File.Exists(path) && Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                items.Add(new VideoItem(path));
        }

        if (items.Count == 0)
            return;

        foreach (var item in items)
        {
            if (_allItems.All(a => !string.Equals(a.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
                _allItems.Add(item);
        }

        ApplyFilter();
        SetQueue(items, 0);
        PlayItem(items[0]);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.Left:
                SkipSeconds(e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? -30 : -10);
                e.Handled = true;
                break;
            case Key.Right:
                SkipSeconds(e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? 30 : 10);
                e.Handled = true;
                break;
            case Key.Up:
                _volume.Value = Math.Min(1, _volume.Value + 0.05);
                e.Handled = true;
                break;
            case Key.Down:
                _volume.Value = Math.Max(0, _volume.Value - 0.05);
                e.Handled = true;
                break;
            case Key.F:
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Key.M:
                ToggleMute();
                e.Handled = true;
                break;
            case Key.N:
                PlayNext(manual: true);
                e.Handled = true;
                break;
            case Key.P:
            case Key.B:
                PlayPrevious();
                e.Handled = true;
                break;
            case Key.R:
                CycleVideoRotation();
                e.Handled = true;
                break;
            case Key.O:
                OpenFile();
                e.Handled = true;
                break;
            case Key.Escape when _fullscreen:
                ToggleFullscreen();
                e.Handled = true;
                break;
        }
    }

    private static Button MakeTextButton(string label, Action action)
    {
        var b = new Button
        {
            Content = label,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x40)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
        };
        b.Click += (_, _) => action();
        return b;
    }

    private sealed record VideoItem(string Path)
    {
        public string Name { get; } = System.IO.Path.GetFileName(Path);
    }

    private sealed record TrackPickerItem(int Id, string Name)
    {
        public override string ToString() => Name;
    }
}
