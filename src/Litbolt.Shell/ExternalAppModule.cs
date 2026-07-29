using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace Litbolt.Shell;

/// <summary>
/// Module that lives in the Folder as a launcher pane and pops out to a standalone EXE.
/// </summary>
public sealed class ExternalAppModule : IAppModule
{
    private readonly string _exePath;
    private readonly Func<string?>? _resolveExe;
    private readonly MediaColor _accent;

    public ExternalAppModule(
        string id,
        string displayName,
        string description,
        string exePath,
        MediaColor? accent = null,
        Func<string?>? resolveExe = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        _exePath = exePath;
        _resolveExe = resolveExe;
        _accent = accent ?? MediaColor.FromRgb(0x5B, 0x8C, 0xFF);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string? StandaloneExePath => ResolveExe();

    public FrameworkElement CreateView()
    {
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = DisplayName,
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfBrushes.White,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var desc = new TextBlock
        {
            Text = Description,
            FontSize = 14,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(0xB0, 0xB8, 0xC8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24),
        };
        Grid.SetRow(desc, 1);
        root.Children.Add(desc);

        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        Grid.SetRow(buttons, 2);

        var open = MakeButton("Open here (standalone window)", _accent, () => Launch(detachedHint: false));
        var pop = MakeButton("Pop out", MediaColor.FromRgb(0x3A, 0x42, 0x55), () => Launch(detachedHint: true));
        buttons.Children.Add(open);
        buttons.Children.Add(pop);
        root.Children.Add(buttons);

        var pathHint = new TextBlock
        {
            Text = ResolveExe() is { } p
                ? $"EXE: {p}"
                : "EXE not found — install/build the standalone app, or set the path in Folder settings.",
            FontSize = 12,
            Foreground = new SolidColorBrush(MediaColor.FromRgb(0x88, 0x90, 0xA0)),
            Margin = new Thickness(0, 20, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(pathHint, 2);
        // place below buttons
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(pathHint, 3);
        root.Children.Add(pathHint);

        return root;
    }

    public void DisposeView(FrameworkElement view) { }

    private void Launch(bool detachedHint)
    {
        var exe = ResolveExe();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            System.Windows.MessageBox.Show(
                $"Could not find {DisplayName}.\n\nExpected:\n{_exePath}",
                "Litbolt's App Folder",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
        });

        if (detachedHint)
        {
            /* Pop-out is the standalone window; Folder tab stays as launcher. */
        }
    }

    private string? ResolveExe()
    {
        var fromCallback = _resolveExe?.Invoke();
        if (!string.IsNullOrWhiteSpace(fromCallback) && File.Exists(fromCallback))
            return fromCallback;
        if (File.Exists(_exePath))
            return _exePath;
        return null;
    }

    private static WpfButton MakeButton(string text, MediaColor bg, Action onClick)
    {
        var btn = new WpfButton
        {
            Content = text,
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 12, 0),
            Background = new SolidColorBrush(bg),
            Foreground = WpfBrushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
