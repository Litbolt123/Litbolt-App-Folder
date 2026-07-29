using System.IO;
using System.Text.Json;

namespace LitboltAppFolder.Services;

public sealed class FolderSettings
{
    public bool AutoCheckUpdates { get; set; } = true;
    public string? LastUpdateCheckUtc { get; set; }
    public string? CatchUpUpdateDismissedVersion { get; set; }

    /// <summary>Override path to LocalMusicHub.exe (optional).</summary>
    public string? MusicHubExePath { get; set; }

    /// <summary>Override path to YouTubeToMp3.exe (optional).</summary>
    public string? DownloaderExePath { get; set; }

    /// <summary>Override path to LocalVideoHub.exe (optional).</summary>
    public string? VideoHubExePath { get; set; }

    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
}

public static class FolderSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string Dir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LitboltAppFolder");

    private static string PathFile => System.IO.Path.Combine(Dir, "settings.json");

    public static FolderSettings Load()
    {
        try
        {
            if (!File.Exists(PathFile))
                return new FolderSettings();
            var json = File.ReadAllText(PathFile);
            return JsonSerializer.Deserialize<FolderSettings>(json, JsonOptions) ?? new FolderSettings();
        }
        catch
        {
            return new FolderSettings();
        }
    }

    public static void Save(FolderSettings settings)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
