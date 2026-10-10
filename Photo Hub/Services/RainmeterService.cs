using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotoHub.Services;

public sealed record RainmeterWidgetSettings(int Size, int Seconds, int Radius);

public sealed record IllustroSkinEntry(string ConfigKey, string DisplayName, string IniPath);

public static class RainmeterService
{
    private const string GoodIniBackupFileName = "Photo Widget.ini.good-2026-10-10";
    private const string MeterPhotoSection = "[MeterPhoto]";
    private const int SizeMin = 150;
    private const int SizeMax = 600;

    private static readonly Regex VarLineRe = new(@"^(Size|Seconds|Radius)=(\d+)\s*$", RegexOptions.IgnoreCase);

    public static RainmeterWidgetSettings ReadWidgetSettings()
    {
        EnsureIniHasMeters();
        var text = File.ReadAllText(PhotoPaths.RainmeterIni);
        int size = 320, seconds = 60, radius = 28;
        var inVariables = false;
        foreach (var line in SplitLines(text))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inVariables = trimmed.Equals("[Variables]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inVariables)
                continue;
            var m = VarLineRe.Match(trimmed);
            if (!m.Success || !int.TryParse(m.Groups[2].Value, out var n))
                continue;
            switch (m.Groups[1].Value.ToUpperInvariant())
            {
                case "SIZE": size = n; break;
                case "SECONDS": seconds = n; break;
                case "RADIUS": radius = n; break;
            }
        }
        return new RainmeterWidgetSettings(Math.Clamp(size, SizeMin, SizeMax), seconds, radius);
    }

    public static void WriteWidgetSettings(RainmeterWidgetSettings s, bool refresh = true)
    {
        var clamped = new RainmeterWidgetSettings(
            Math.Clamp(s.Size, SizeMin, SizeMax),
            s.Seconds,
            s.Radius);

        EnsureIniHasMeters();
        var path = PhotoPaths.RainmeterIni;
        var original = File.ReadAllText(path);
        if (!original.Contains(MeterPhotoSection, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to write Photo Widget.ini without [MeterPhoto].");

        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var updated = UpdateVariablesSection(original, clamped, newline);
        if (!updated.Contains(MeterPhotoSection, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to write Photo Widget.ini without [MeterPhoto].");

        File.WriteAllText(path, updated, Encoding.UTF8);
        if (refresh)
            RefreshPhotoWidget();
    }

    public static void WriteSize(int size) =>
        WriteWidgetSettings(ReadWidgetSettings() with { Size = size });

    public static void EnsureIniHasMeters()
    {
        var path = PhotoPaths.RainmeterIni;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            if (text.Contains(MeterPhotoSection, StringComparison.Ordinal))
                return;
        }

        var good = Path.Combine(dir, GoodIniBackupFileName);
        if (!File.Exists(good))
            throw new FileNotFoundException($"Photo Widget.ini is missing [MeterPhoto] and backup was not found.", good);

        File.Copy(good, path, overwrite: true);
        HubLog.Info($"Restored Photo Widget.ini from {GoodIniBackupFileName}.");
    }

    private static string UpdateVariablesSection(string text, RainmeterWidgetSettings s, string newline)
    {
        var lines = SplitLines(text);
        var output = new List<string>(lines.Length);
        var inVariables = false;
        var foundSize = false;
        var foundSeconds = false;
        var foundRadius = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inVariables)
                    AppendMissingVariables(output, s, ref foundSize, ref foundSeconds, ref foundRadius);
                inVariables = trimmed.Equals("[Variables]", StringComparison.OrdinalIgnoreCase);
                output.Add(line);
                continue;
            }

            if (inVariables)
            {
                if (VarLineRe.Match(trimmed) is { Success: true } m)
                {
                    switch (m.Groups[1].Value.ToUpperInvariant())
                    {
                        case "SIZE":
                            output.Add($"Size={s.Size}");
                            foundSize = true;
                            continue;
                        case "SECONDS":
                            output.Add($"Seconds={s.Seconds}");
                            foundSeconds = true;
                            continue;
                        case "RADIUS":
                            output.Add($"Radius={s.Radius}");
                            foundRadius = true;
                            continue;
                    }
                }
            }

            output.Add(line);
        }

        if (inVariables)
            AppendMissingVariables(output, s, ref foundSize, ref foundSeconds, ref foundRadius);

        return string.Join(newline, output);
    }

    private static void AppendMissingVariables(
        List<string> output,
        RainmeterWidgetSettings s,
        ref bool foundSize,
        ref bool foundSeconds,
        ref bool foundRadius)
    {
        if (!foundSize)
            output.Add($"Size={s.Size}");
        if (!foundSeconds)
            output.Add($"Seconds={s.Seconds}");
        if (!foundRadius)
            output.Add($"Radius={s.Radius}");
    }

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    public static void RefreshPhotoWidget() => RunBang("!Refresh \"Photo Widget\"");

    public static void SetPhotoWidgetActive(bool active) =>
        RunBang(active
            ? "!ActivateConfig \"Photo Widget\\Photo Widget.ini\""
            : "!DeactivateConfig \"Photo Widget\\Photo Widget.ini\"");

    public static IReadOnlyList<IllustroSkinEntry> ListIllustroSkins()
    {
        if (!Directory.Exists(PhotoPaths.IllustroRoot))
            return [];
        var list = new List<IllustroSkinEntry>();
        foreach (var dir in Directory.GetDirectories(PhotoPaths.IllustroRoot))
        {
            var name = Path.GetFileName(dir);
            foreach (var ini in Directory.GetFiles(dir, "*.ini"))
            {
                var file = Path.GetFileName(ini);
                var key = $"illustro\\{name}\\{file}";
                list.Add(new IllustroSkinEntry(key, $"{name} — {file}", ini));
            }
        }
        return list.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void SetIllustroActive(string configKey, bool active) =>
        RunBang(active ? $"!ActivateConfig \"{configKey}\"" : $"!DeactivateConfig \"{configKey}\"");

    /// <summary>Full path from @Resources\current-photo.inc (CurrentPhoto=).</summary>
    public static string? ReadCurrentWidgetPhotoPath()
    {
        if (!File.Exists(PhotoPaths.RainmeterCurrentPhotoInc))
            return null;
        foreach (var line in File.ReadAllLines(PhotoPaths.RainmeterCurrentPhotoInc))
        {
            var t = line.Trim();
            if (t.StartsWith("CurrentPhoto=", StringComparison.OrdinalIgnoreCase))
            {
                var value = t["CurrentPhoto=".Length..].Trim();
                if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
                    value = value[1..^1];
                return value.Length > 0 ? value : null;
            }
        }
        return null;
    }

    public static string? ReadCurrentWidgetPhotoFileName()
    {
        var path = ReadCurrentWidgetPhotoPath();
        return string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
    }

    public static void EnsureCurrentPhotoSkinHook()
    {
        // Skin ships with OnChangeAction → current-photo.inc; no ini surgery needed.
    }

    private static void RunBang(string bang)
    {
        if (!File.Exists(PhotoPaths.RainmeterExe))
            return;
        Process.Start(new ProcessStartInfo
        {
            FileName = PhotoPaths.RainmeterExe,
            Arguments = bang,
            UseShellExecute = true,
        });
    }
}
