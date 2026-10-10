using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PhotoHub.Services;

public static class LitboltTheme
{
    public static readonly Color Bg = Color.FromRgb(0x12, 0x14, 0x1A);
    public static readonly Color Panel = Color.FromRgb(0x16, 0x1A, 0x22);
    public static readonly Color InputBg = Color.FromRgb(0x1A, 0x1E, 0x28);
    public static readonly Color ButtonBg = Color.FromRgb(0x2A, 0x33, 0x48);
    public static readonly Color Accent = Color.FromRgb(0xE8, 0xA0, 0x4A);
    public static readonly Color Muted = Color.FromRgb(0x88, 0x90, 0xA0);
    public static readonly Color Text = Colors.White;

    public static Button MakeButton(string text, bool primary = false)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(primary ? Accent : ButtonBg),
            Foreground = new SolidColorBrush(primary ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Text),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
    }

    public static TextBox MakeTextBox(string? text = null) => new()
    {
        Text = text ?? "",
        Padding = new Thickness(8, 6, 8, 6),
        Background = new SolidColorBrush(InputBg),
        Foreground = new SolidColorBrush(Text),
        BorderBrush = new SolidColorBrush(ButtonBg),
    };
}
