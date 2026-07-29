using System.IO;
using System.Windows;
using Litbolt.Shell;
using Litbolt.Shell.Tray;
using LitboltAppFolder.Services;

namespace LitboltAppFolder;

public partial class App : Application
{
    public const string AutoStartRegistryName = "LitboltAppFolder";
    public const string MutexName = @"Global\LitboltAppFolder_SingleInstance";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LitboltAppFolder");

    private static Mutex? _singleInstanceMutex;

    public static FolderSettings Settings { get; private set; } = new();

    public bool BypassMainWindowCloseCancel { get; set; }

    public bool StartMinimizedToTray { get; private set; }

    public void ExitForInstallerUpgrade()
    {
        BypassMainWindowCloseCancel = true;

        try
        {
            foreach (Window w in Windows.Cast<Window>().ToList())
            {
                if (ReferenceEquals(w, MainWindow))
                    continue;
                try { w.Close(); } catch { /* ignore */ }
            }
        }
        catch
        {
            /* ignore */
        }

        if (MainWindow is MainWindow mw)
            mw.RequestForceClose();
        Shutdown(0);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceService.TryBecomePrimaryInstance(MutexName, out _singleInstanceMutex))
        {
            SingleInstanceService.NotifyPrimaryInstance(DataDirectory);
            Shutdown();
            return;
        }

        Settings = FolderSettingsStore.Load();
        Settings.StartWithWindows = AutoStartService.IsEnabled(AutoStartRegistryName);
        if (Settings.StartWithWindows)
            Settings.MinimizeToTray = true;

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
