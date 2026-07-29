using System.IO;
using System.Text.Json;

namespace LocalVideoHub;

internal enum VideoRepeatMode
{
    Off,
    All,
    One,
}

internal sealed class VideoSettingsData
{
    public string? LibraryFolder { get; set; }
    public bool RecursiveScan { get; set; }
    public double Volume { get; set; } = 0.8;
    public double Speed { get; set; } = 1.0;
    public string RepeatMode { get; set; } = "off";
    public List<string> RecentFiles { get; set; } = [];
}

internal static class VideoSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string SettingsDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalVideoHub");

    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    private static string LegacyFolderPath => Path.Combine(SettingsDir, "settings.txt");

    private static string LegacyLitboltFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LitboltVideo",
            "settings.txt");

    public static VideoSettingsData Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<VideoSettingsData>(json, JsonOptions) ?? new VideoSettingsData();
            }

            var data = new VideoSettingsData();
            var folder = LoadLegacyFolder();
            if (!string.IsNullOrWhiteSpace(folder))
                data.LibraryFolder = folder;
            return data;
        }
        catch
        {
            return new VideoSettingsData();
        }
    }

    public static void Save(VideoSettingsData data)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            /* ignore */
        }
    }

    public static VideoRepeatMode ParseRepeat(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "all" => VideoRepeatMode.All,
            "one" => VideoRepeatMode.One,
            _ => VideoRepeatMode.Off,
        };

    public static string RepeatToString(VideoRepeatMode mode) =>
        mode switch
        {
            VideoRepeatMode.All => "all",
            VideoRepeatMode.One => "one",
            _ => "off",
        };

    private static string? LoadLegacyFolder()
    {
        try
        {
            if (File.Exists(LegacyFolderPath))
                return File.ReadAllText(LegacyFolderPath).Trim();
            if (File.Exists(LegacyLitboltFolderPath))
                return File.ReadAllText(LegacyLitboltFolderPath).Trim();
            return null;
        }
        catch
        {
            return null;
        }
    }
}
