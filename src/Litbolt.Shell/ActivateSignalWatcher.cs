namespace Litbolt.Shell;

using System.IO;

/// <summary>Watches %LocalAppData% data dir for activate.signal from a second instance.</summary>
public sealed class ActivateSignalWatcher : IDisposable
{
    private readonly string _dataDirectory;
    private readonly Action _onActivate;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public ActivateSignalWatcher(string dataDirectory, Action onActivate)
    {
        _dataDirectory = dataDirectory;
        _onActivate = onActivate;
    }

    public void Start()
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            _watcher = new FileSystemWatcher(_dataDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnSignal;
            _watcher.Changed += OnSignal;
        }
        catch
        {
            /* ignore */
        }
    }

    private void OnSignal(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(Path.GetFileName(e.FullPath), SingleInstanceService.ActivateSignalFileName,
                StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            _onActivate();
        }
        catch
        {
            /* ignore */
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
