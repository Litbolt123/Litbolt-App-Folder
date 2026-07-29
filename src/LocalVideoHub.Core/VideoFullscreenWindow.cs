using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LocalVideoHub;

/// <summary>Borderless window that shows only the video surface (not the host app shell).</summary>
internal sealed class VideoFullscreenWindow : Window
{
    public VideoFullscreenWindow(FrameworkElement videoSurface)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        Background = Brushes.Black;
        ShowInTaskbar = false;
        Topmost = true;
        Content = videoSurface;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                RequestExit();
                e.Handled = true;
            }
        };

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
                RequestExit();
        };
    }

    public event Action? ExitRequested;

    public void RequestExit() => ExitRequested?.Invoke();

    protected override void OnClosed(EventArgs e)
    {
        Content = null;
        base.OnClosed(e);
    }
}
