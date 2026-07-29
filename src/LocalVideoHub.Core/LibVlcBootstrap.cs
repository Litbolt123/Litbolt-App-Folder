using LibVLCSharp.Shared;

namespace LocalVideoHub;

internal static class LibVlcBootstrap
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static bool _available;
    private static LibVLC? _libVlc;

    public static bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public static LibVLC? Instance
    {
        get
        {
            EnsureInitialized();
            return _available ? _libVlc : null;
        }
    }

    private static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized)
                return;

            _initialized = true;
            try
            {
                Core.Initialize();
                _libVlc = new LibVLC("--no-video-title-show");
                _available = true;
            }
            catch
            {
                _libVlc?.Dispose();
                _libVlc = null;
                _available = false;
            }
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            _libVlc?.Dispose();
            _libVlc = null;
            _available = false;
            _initialized = false;
        }
    }
}
