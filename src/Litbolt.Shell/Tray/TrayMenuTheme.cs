using System.Drawing;
using System.Windows.Forms;

namespace Litbolt.Shell.Tray;

public static class TrayMenuTheme
{
    private static readonly Color Background = Color.FromArgb(0x16, 0x1A, 0x22);
    private static readonly Color Foreground = Color.White;
    private static readonly Color Hover = Color.FromArgb(0x2A, 0x33, 0x48);
    private static readonly Color Separator = Color.FromArgb(0x2A, 0x30, 0x40);

    public static void Apply(ContextMenuStrip menu)
    {
        menu.BackColor = Background;
        menu.ForeColor = Foreground;
        menu.RenderMode = ToolStripRenderMode.System;
        menu.ShowImageMargin = false;
        menu.Font = new Font("Segoe UI", 9f);

        foreach (ToolStripItem item in menu.Items)
        {
            item.BackColor = Background;
            item.ForeColor = Foreground;
        }
    }

    public static ToolStripMenuItem CreateHeader(string text) =>
        new(text)
        {
            Enabled = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x88, 0x90, 0xA0),
        };
}
