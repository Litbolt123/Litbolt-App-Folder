using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PhotoHub.Services;

public sealed record RainmeterWidgetSettings(int Size, int Seconds, int Radius);

public sealed record IllustroSkinEntry(string ConfigKey, string DisplayName, string IniPath);

public static class RainmeterService
{
    private static readonly Regex VarRe = new(@"^(Size|Seconds|Radius)=(\d+)\s*$", RegexOptions.Multiline);

    public static RainmeterWidgetSettings ReadWidgetSettings()
    {
        if (!File.Exists(PhotoPaths.RainmeterIni))
            return new RainmeterWidgetSettings(320, 60, 28);
        var text = File.ReadAllText(PhotoPaths.RainmeterIni);
        int size = 320, seconds = 60, radius = 28;
        foreach (Match m in VarRe.Matches(text))
        {
            if (!int.TryParse(m.Groups[2].Value, out var n))
                continue;
            switch (m.Groups[1].Value)
            {
                case "Size": size = n; break;
                case "Seconds": seconds = n; break;
                case "Radius": radius = n; break;
            }
        }
        return new RainmeterWidgetSettings(size, seconds, radius);
    }

    public static void WriteWidgetSettings(RainmeterWidgetSettings s, bool refresh = true)
    {
        var text = File.Exists(PhotoPaths.RainmeterIni)
            ? File.ReadAllText(PhotoPaths.RainmeterIni)
            : "[Variables]\n";
        text = SetVar(text, "Size", s.Size);
        text = SetVar(text, "Seconds", s.Seconds);
        text = SetVar(text, "Radius", s.Radius);
        Directory.CreateDirectory(Path.GetDirectoryName(PhotoPaths.RainmeterIni)!);
        File.WriteAllText(PhotoPaths.RainmeterIni, text);
        if (refresh)
            RefreshPhotoWidget();
    }

    public static void WriteSize(int size) =>
        WriteWidgetSettings(ReadWidgetSettings() with { Size = size });

    private static string SetVar(string text, string name, int value)
    {
        var line = $"{name}={value}";
        var pat = new Regex($@"^{name}=\d+\s*$", RegexOptions.Multiline);
        if (pat.IsMatch(text))
            return pat.Replace(text, line);
        if (text.Contains("[Variables]", StringComparison.Ordinal))
            return text.Replace("[Variables]", $"[Variables]\n{line}", StringComparison.Ordinal);
        return text + $"\n{line}\n";
    }

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

    public static string? ReadCurrentWidgetPhotoFileName()
    {
        if (!File.Exists(PhotoPaths.RainmeterCurrentPhotoIni))
            return null;
        foreach (var line in File.ReadAllLines(PhotoPaths.RainmeterCurrentPhotoIni))
        {
            var t = line.Trim();
            if (t.StartsWith("File=", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(t["File=".Length..].Trim());
            if (t.Length > 0 && !t.StartsWith('[') && !t.Contains('='))
                return Path.GetFileName(t);
        }
        return null;
    }

    public static void EnsureCurrentPhotoSkinHook()
    {
        if (!File.Exists(PhotoPaths.RainmeterIni))
            return;
        var text = File.ReadAllText(PhotoPaths.RainmeterIni);
        if (text.Contains("OnChangeAction=", StringComparison.Ordinal) &&
            text.Contains("current-photo.txt", StringComparison.Ordinal))
            return;
        const string hook = "OnChangeAction=[!WriteKeyValue State File \"[MeasurePhoto]\" \"#@#current-photo.txt\"]";
        if (text.Contains("[MeasurePhoto]", StringComparison.Ordinal))
        {
            text = text.Replace(
                "UpdateDivider=#Seconds#",
                $"UpdateDivider=#Seconds#\n{hook}");
            Directory.CreateDirectory(Path.GetDirectoryName(PhotoPaths.RainmeterCurrentPhotoIni)!);
            File.WriteAllText(PhotoPaths.RainmeterIni, text);
        }
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
