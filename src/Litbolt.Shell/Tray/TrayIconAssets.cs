using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Litbolt.Shell.Tray;

public static class TrayIconAssets
{
    public static Icon CreateIcon()
    {
        foreach (var path in CandidateIconPaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                using var icon = new Icon(path);
                return (Icon)icon.Clone();
            }
            catch
            {
                /* try next */
            }
        }

        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"))?.Stream;
            if (stream is not null)
            {
                using (stream)
                {
                    using var icon = new Icon(stream);
                    return (Icon)icon.Clone();
                }
            }
        }
        catch
        {
            /* fall through */
        }

        return SystemIcons.Application;
    }

    public static BitmapSource CreateWindowIcon()
    {
        foreach (var path in CandidateIconPaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                using var stream = File.OpenRead(path);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
            catch
            {
                /* try next */
            }
        }

        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"))?.Stream;
            if (stream is not null)
            {
                using (stream)
                {
                    var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    frame.Freeze();
                    return frame;
                }
            }
        }
        catch
        {
            /* fall through */
        }

        using var icon = CreateIcon();
        using var mem = new MemoryStream();
        icon.Save(mem);
        mem.Position = 0;
        var drawn = BitmapFrame.Create(mem, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        drawn.Freeze();
        return drawn;
    }

    private static IEnumerable<string> CandidateIconPaths()
    {
        var paths = new List<string>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        paths.Add(Path.Combine(baseDir, "app.ico"));

        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            var exeDir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrWhiteSpace(exeDir))
                paths.Add(Path.Combine(exeDir, "app.ico"));
        }

        var asm = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(asm))
        {
            var asmDir = Path.GetDirectoryName(asm);
            if (!string.IsNullOrWhiteSpace(asmDir))
                paths.Add(Path.Combine(asmDir, "app.ico"));
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
