using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LocalVideoHub;

/// <summary>LMH-style transport icons and buttons for the video player.</summary>
internal static class VideoTransportUi
{
    public static readonly SolidColorBrush Accent = Freeze(new SolidColorBrush(Color.FromRgb(0x5B, 0x8C, 0xFF)));
    public static readonly SolidColorBrush AccentHover = Freeze(new SolidColorBrush(Color.FromRgb(0x6B, 0x9A, 0xFF)));
    public static readonly SolidColorBrush TextPrimary = Freeze(new SolidColorBrush(Colors.White));
    public static readonly SolidColorBrush TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xB8, 0xC8)));
    public static readonly SolidColorBrush ToolbarHover = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x48)));

    private static readonly Geometry PrevIcon = Parse("M5,5 V19 H7.2 V12.8 L17.8,19.5 V4.5 L7.2,11.2 V5 Z");
    private static readonly Geometry NextIcon = Parse("M6.2,4.5 L16.8,11.2 V5 H19 V19 H16.8 V12.8 L6.2,19.5 Z");
    private static readonly Geometry PlayIcon = Parse("M8,5 L19,12 L8,19 Z");
    private static readonly Geometry PauseIcon = Parse("M6,5 H10 V19 H6 Z M14,5 H18 V19 H14 Z");
    private static readonly Geometry RepeatIcon = Parse("M7,7 H17 V10.2 L21,6 17,1.8 V5 H5 V11 H7 V7 Z M17,17 H7 V13.8 L3,18 7,22.2 V19 H19 V13 H17 V17 Z");
    private static readonly Geometry StopIcon = Parse("M6,6 H18 V18 H6 Z");

    public static Button CreateIconButton(Geometry icon, string tooltip, Action onClick, double size = 36)
    {
        var btn = new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 6, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ToolTip = tooltip,
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = CreateIconPath(icon, 18),
        };
        btn.Click += (_, _) => onClick();
        StyleToolbarButton(btn);
        return btn;
    }

    public static (Button Button, Path PlayIcon, Path PauseIcon) CreatePlayPauseButton(Action onClick)
    {
        var play = CreateIconPath(PlayIcon, 16);
        play.Margin = new Thickness(1, 0, 0, 0);
        var pause = CreateIconPath(PauseIcon, 16);
        pause.Visibility = Visibility.Collapsed;

        var grid = new Grid { Width = 18, Height = 18 };
        grid.Children.Add(play);
        grid.Children.Add(pause);

        var btn = new Button
        {
            Width = 44,
            Height = 44,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 6, 0),
            Background = Accent,
            BorderThickness = new Thickness(0),
            ToolTip = "Play / Pause",
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = grid,
        };
        btn.Click += (_, _) => onClick();
        btn.MouseEnter += (_, _) => btn.Background = AccentHover;
        btn.MouseLeave += (_, _) => btn.Background = Accent;

        return (btn, play, pause);
    }

    public static Button CreatePrevButton(Action onClick) => CreateIconButton(PrevIcon, "Previous", onClick);
    public static Button CreateNextButton(Action onClick) => CreateIconButton(NextIcon, "Next", onClick);
    public static Button CreateStopButton(Action onClick) => CreateIconButton(StopIcon, "Stop", onClick);
    public static Button CreateRepeatButton(Action onClick) => CreateIconButton(RepeatIcon, "Repeat", onClick, 36);

    public static TextBlock CreateTimeLabel() =>
        new()
        {
            Foreground = TextMuted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "0:00",
        };

    private static Path CreateIconPath(Geometry data, double size) =>
        new()
        {
            Data = data,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Fill = TextPrimary,
            SnapsToDevicePixels = true,
        };

    private static void StyleToolbarButton(Button btn)
    {
        btn.MouseEnter += (_, _) => btn.Background = ToolbarHover;
        btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
    }

    private static Geometry Parse(string data) => Geometry.Parse(data);

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
