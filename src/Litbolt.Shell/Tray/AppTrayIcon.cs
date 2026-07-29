using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace Litbolt.Shell.Tray;

public sealed class AppTrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly string _title;
    private Window? _window;
    private bool _disposed;

    public AppTrayIcon(string title, Action<ContextMenuStrip> configureMenu)
    {
        _title = title;
        _ownedIcon = TrayIconAssets.CreateIcon();
        _icon = new NotifyIcon
        {
            Text = title,
            Icon = _ownedIcon,
            Visible = false,
        };

        var menu = new ContextMenuStrip();
        configureMenu(menu);
        TrayMenuTheme.Apply(menu);
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void Attach(Window window) => _window = window;

    public void Show() => _icon.Visible = true;

    public void Hide() => _icon.Visible = false;

    public void MinimizeToTray(string? balloonMessage = null)
    {
        Show();
        _window?.Hide();
        ShowBalloonTip(balloonMessage);
    }

    public void ShowBalloonTip(string? message, string? title = null, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            _icon.ShowBalloonTip(4000, title ?? _title, message, icon);
        }
        catch
        {
            /* ignore */
        }
    }

    public void ShowMainWindow()
    {
        if (_window is null)
            return;

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void RequestQuit()
    {
        if (_window is ITrayHost host)
            host.RequestForceClose();

        Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _icon.Visible = false;
        _icon.Dispose();
        _ownedIcon.Dispose();
        _disposed = true;
    }
}

/// <summary>Implemented by main windows that support close-to-tray.</summary>
public interface ITrayHost
{
    void RequestForceClose();
    bool ShouldMinimizeToTrayOnClose { get; }
    void OnTrayPreferenceChanged();
}
