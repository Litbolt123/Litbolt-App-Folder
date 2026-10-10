using System.IO;
using System.Windows;
using Litbolt.Shell;
using Litbolt.Shell.Tray;

namespace PhotoHub;

public partial class App : Application
{
    public const string AutoStartRegistryName = "PhotoHub";
    public const string MutexName = @"Global\PhotoHub_SingleInstance";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoHub");

    private static Mutex? _singleInstanceMutex;

    public bool BypassMainWindowCloseCancel { get; set; }

    public bool StartMinimizedToTray { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceService.TryBecomePrimaryInstance(MutexName, out _singleInstanceMutex))
        {
            SingleInstanceService.NotifyPrimaryInstance(DataDirectory);
            Shutdown();
            return;
        }

        Directory.CreateDirectory(DataDirectory);
        StartMinimizedToTray = AutoStartService.ArgsRequestTray(e.Args);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { /* ignore */ }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
