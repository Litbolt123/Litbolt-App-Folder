using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoHub.Services;

/// <summary>LRU cache for decoded thumbnails; decode at background dispatcher priority.</summary>
public sealed class ThumbnailCache
{
    public static ThumbnailCache Shared { get; } = new();

    private const int MaxEntries = 220;
    private readonly object _gate = new();
    private readonly Dictionary<string, BitmapSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();

    public async Task<BitmapSource?> GetAsync(string? path, int decodeWidth = 160, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var hit))
            {
                Touch(path);
                return hit;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var bitmap = await dispatcher.InvokeAsync(
            () => DecodeOnUiThread(path, decodeWidth),
            DispatcherPriority.Background,
            cancellationToken);

        if (bitmap is null)
            return null;

        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var existing))
                return existing;
            _cache[path] = bitmap;
            _lru.AddFirst(path);
            while (_lru.Count > MaxEntries)
            {
                var old = _lru.Last!.Value;
                _lru.RemoveLast();
                _cache.Remove(old);
            }
        }

        return bitmap;
    }

    private static BitmapSource? DecodeOnUiThread(string path, int decodeWidth)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path, UriKind.Absolute);
            bi.DecodePixelWidth = decodeWidth;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }

    private void Touch(string path)
    {
        _lru.Remove(path);
        _lru.AddFirst(path);
    }
}
