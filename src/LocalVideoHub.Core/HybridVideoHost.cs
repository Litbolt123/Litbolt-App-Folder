using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace LocalVideoHub;

public readonly record struct MediaTrackInfo(int Id, string Name);

/// <summary>LibVLC-first playback with MediaElement fallback when LibVLC is unavailable or fails.</summary>
internal sealed class HybridVideoHost : UserControl
{
    private readonly MediaElement _mediaElement;
    private readonly VideoView? _vlcView;
    private readonly VlcMediaPlayer? _vlcPlayer;
    private VideoBackend _backend = VideoBackend.MediaElement;
    private string? _currentPath;
    private bool _triedMediaElementFallback;
    private double _volume = 0.8;
    private double _speed = 1.0;

    public HybridVideoHost()
    {
        var grid = new Grid();
        _mediaElement = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Stretch = Stretch.Uniform,
            ScrubbingEnabled = true,
        };
        _mediaElement.MediaOpened += (_, e) => MediaOpened?.Invoke(this, e);
        _mediaElement.MediaEnded += (_, e) => MediaEnded?.Invoke(this, e);
        _mediaElement.MediaFailed += (_, e) => OnMediaElementFailed(e);
        _mediaElement.MouseLeftButtonDown += (_, e) => VideoMouseLeftButtonDown?.Invoke(this, e);
        grid.Children.Add(_mediaElement);

        if (LibVlcBootstrap.Instance is { } libVlc)
        {
            _vlcPlayer = new VlcMediaPlayer(libVlc);
            _vlcView = new VideoView { MediaPlayer = _vlcPlayer };
            _vlcView.Visibility = Visibility.Collapsed;
            _vlcPlayer.EndReached += (_, _) =>
                Dispatcher.BeginInvoke(() => MediaEnded?.Invoke(this, new RoutedEventArgs()));
            _vlcPlayer.Playing += (_, _) =>
                Dispatcher.BeginInvoke(() => MediaOpened?.Invoke(this, new RoutedEventArgs()));
            _vlcPlayer.EncounteredError += (_, _) =>
                Dispatcher.BeginInvoke(() =>
                {
                    if (TryFallbackToMediaElement())
                        return;

                    RaiseFailed("LibVLC playback error.");
                });
            _vlcView.MouseLeftButtonDown += (_, e) => VideoMouseLeftButtonDown?.Invoke(this, e);
            grid.Children.Add(_vlcView);
        }

        Content = grid;
    }

    public FrameworkElement ActiveVisual => _backend == VideoBackend.LibVlc && _vlcView is not null
        ? _vlcView
        : _mediaElement;

    public FrameworkElement DetachActiveVisual()
    {
        var visual = ActiveVisual;
        if (visual.Parent is Panel panel)
            panel.Children.Remove(visual);
        return visual;
    }

    public void AttachActiveVisual()
    {
        var grid = (Grid)Content;
        var visual = ActiveVisual;
        if (!grid.Children.Contains(visual))
            grid.Children.Add(visual);
    }

    public Uri? Source => _backend == VideoBackend.MediaElement ? _mediaElement.Source : null;

    public bool HasDuration
    {
        get
        {
            if (_backend == VideoBackend.LibVlc && _vlcPlayer is not null)
                return _vlcPlayer.Length > 0;

            return _mediaElement.NaturalDuration.HasTimeSpan;
        }
    }

    public TimeSpan Position
    {
        get
        {
            if (_backend == VideoBackend.LibVlc && _vlcPlayer is not null)
                return TimeSpan.FromMilliseconds(Math.Max(0, _vlcPlayer.Time));

            return _mediaElement.Position;
        }
        set
        {
            if (_backend == VideoBackend.LibVlc && _vlcPlayer is not null)
                _vlcPlayer.Time = (long)Math.Max(0, value.TotalMilliseconds);
            else
                _mediaElement.Position = value;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (_backend == VideoBackend.LibVlc && _vlcPlayer is not null && _vlcPlayer.Length > 0)
                return TimeSpan.FromMilliseconds(_vlcPlayer.Length);

            return _mediaElement.NaturalDuration.HasTimeSpan
                ? _mediaElement.NaturalDuration.TimeSpan
                : TimeSpan.Zero;
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            _mediaElement.Volume = _volume;
            if (_vlcPlayer is not null)
                _vlcPlayer.Volume = (int)Math.Round(_volume * 100);
        }
    }

    public double SpeedRatio
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25, 4);
            _mediaElement.SpeedRatio = _speed;
            if (_vlcPlayer is not null)
                _vlcPlayer.SetRate((float)_speed);
        }
    }

    public bool IsLibVlcActive => _backend == VideoBackend.LibVlc;

    public bool LibVlcAvailable => _vlcPlayer is not null;

    public event RoutedEventHandler? MediaOpened;
    public event RoutedEventHandler? MediaEnded;
    public event EventHandler<Exception?>? MediaFailed;
    public event MouseButtonEventHandler? VideoMouseLeftButtonDown;

    public bool HasSource => !string.IsNullOrWhiteSpace(_currentPath);

    public void Open(string path)
    {
        _currentPath = path;
        _triedMediaElementFallback = false;
        if (_vlcPlayer is not null)
            OpenLibVlc(path);
        else
            OpenMediaElement(path);
    }

    public void Play()
    {
        if (_backend == VideoBackend.LibVlc)
            _vlcPlayer?.Play();
        else
            _mediaElement.Play();
    }

    public void Pause()
    {
        if (_backend == VideoBackend.LibVlc)
            _vlcPlayer?.Pause();
        else
            _mediaElement.Pause();
    }

    public void Stop()
    {
        if (_backend == VideoBackend.LibVlc)
            _vlcPlayer?.Stop();
        else
            _mediaElement.Stop();
    }

    public void Close()
    {
        Stop();
        if (_backend == VideoBackend.LibVlc)
            _vlcPlayer?.Media?.Dispose();
        else
            _mediaElement.Close();
        _currentPath = null;
    }

    public void Shutdown()
    {
        Close();
        _vlcPlayer?.Dispose();
    }

    public bool TryFallbackToLibVlc()
    {
        if (_vlcPlayer is null || string.IsNullOrWhiteSpace(_currentPath) || _backend == VideoBackend.LibVlc)
            return false;

        _triedMediaElementFallback = false;
        OpenLibVlc(_currentPath);
        return _backend == VideoBackend.LibVlc;
    }

    public bool TryFallbackToMediaElement()
    {
        if (string.IsNullOrWhiteSpace(_currentPath) || _triedMediaElementFallback)
            return false;

        _triedMediaElementFallback = true;
        OpenMediaElement(_currentPath);
        return _backend == VideoBackend.MediaElement;
    }

    public void LoadSubtitleFile(string path)
    {
        if (_backend != VideoBackend.LibVlc || _vlcPlayer?.Media is null || !File.Exists(path))
            return;

        _vlcPlayer.AddSlave(MediaSlaveType.Subtitle, path, true);
    }

    public IReadOnlyList<MediaTrackInfo> GetAudioTracks()
    {
        if (_backend != VideoBackend.LibVlc || _vlcPlayer is null)
            return [];

        return _vlcPlayer.AudioTrackDescription
            .Where(t => t.Id >= 0)
            .Select(t => new MediaTrackInfo(t.Id, string.IsNullOrWhiteSpace(t.Name) ? $"Track {t.Id}" : t.Name))
            .ToList();
    }

    public IReadOnlyList<MediaTrackInfo> GetSubtitleTracks()
    {
        if (_backend != VideoBackend.LibVlc || _vlcPlayer is null)
            return [];

        return _vlcPlayer.SpuDescription
            .Where(t => t.Id >= 0)
            .Select(t => new MediaTrackInfo(t.Id, string.IsNullOrWhiteSpace(t.Name) ? $"Subtitle {t.Id}" : t.Name))
            .ToList();
    }

    public int CurrentAudioTrackId =>
        _backend == VideoBackend.LibVlc && _vlcPlayer is not null ? _vlcPlayer.AudioTrack : -1;

    public int CurrentSubtitleTrackId =>
        _backend == VideoBackend.LibVlc && _vlcPlayer is not null ? _vlcPlayer.Spu : -1;

    public void SetAudioTrack(int trackId)
    {
        if (_backend != VideoBackend.LibVlc || _vlcPlayer is null || trackId < 0)
            return;

        _vlcPlayer.SetAudioTrack(trackId);
    }

    public void SetSubtitleTrack(int trackId)
    {
        if (_backend != VideoBackend.LibVlc || _vlcPlayer is null)
            return;

        _vlcPlayer.SetSpu(trackId);
    }

    private void OpenMediaElement(string path)
    {
        SwitchBackend(VideoBackend.MediaElement);
        _mediaElement.Source = new Uri(path);
        _mediaElement.Play();
    }

    private void OpenLibVlc(string path)
    {
        if (_vlcPlayer is null)
            return;

        SwitchBackend(VideoBackend.LibVlc);
        _vlcPlayer.Stop();
        _vlcPlayer.Media?.Dispose();

        var media = new Media(LibVlcBootstrap.Instance!, path, FromType.FromPath);
        TryAttachSubtitle(media, path);
        _vlcPlayer.Media = media;
        _vlcPlayer.Play();
    }

    private static void TryAttachSubtitle(Media media, string videoPath)
    {
        var folder = Path.GetDirectoryName(videoPath);
        var baseName = Path.GetFileNameWithoutExtension(videoPath);
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(baseName))
            return;

        foreach (var ext in new[] { ".srt", ".vtt", ".ass" })
        {
            var candidate = Path.Combine(folder, baseName + ext);
            if (!File.Exists(candidate))
                continue;

            var subPath = candidate.Replace('\\', '/');
            media.AddOption($":sub-file={subPath}");
            break;
        }
    }

    private void SwitchBackend(VideoBackend backend)
    {
        _backend = backend;
        _mediaElement.Visibility = backend == VideoBackend.MediaElement
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_vlcView is not null)
            _vlcView.Visibility = backend == VideoBackend.LibVlc ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMediaElementFailed(ExceptionRoutedEventArgs e)
    {
        if (TryFallbackToLibVlc())
            return;

        MediaFailed?.Invoke(this, e.ErrorException);
    }

    private void RaiseFailed(string message) =>
        MediaFailed?.Invoke(this, new InvalidOperationException(message));

    private enum VideoBackend
    {
        MediaElement,
        LibVlc,
    }
}
