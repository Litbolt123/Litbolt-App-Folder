using System.IO;
using System.Text.Json;
using Litbolt.Shell.Updates;

namespace LocalVideoHub;

public sealed class VideoHubSettings
{
    public bool AutoCheckUpdates { get; set; } = true;
    public string? LastUpdateCheckUtc { get; set; }
    public string? CatchUpUpdateDismissedVersion { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
}

public static class VideoHubSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string Dir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalVideoHub");

    private static string PathFile => Path.Combine(Dir, "app-settings.json");

    public static VideoHubSettings Load()
    {
        try
        {
            if (!File.Exists(PathFile))
                return new VideoHubSettings();
            return JsonSerializer.Deserialize<VideoHubSettings>(File.ReadAllText(PathFile), JsonOptions)
                   ?? new VideoHubSettings();
        }
        catch
        {
            return new VideoHubSettings();
        }
    }

    public static void Save(VideoHubSettings settings)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(settings, JsonOptions));
    }
}

public static class VideoHubUpdates
{
    public const string GitHubRepo = "Litbolt123/Local-Video-Hub";
    public const string InstallerPrefix = "LocalVideoHub-Setup-";

    public static GitHubUpdateClient Client { get; } =
        new(GitHubRepo, InstallerPrefix, "LocalVideoHub-UpdateCheck/1.0");

    public static bool ShouldShowBanner(VideoHubSettings settings, string latestVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion))
            return false;
        var dismissed = settings.CatchUpUpdateDismissedVersion ?? "";
        return !string.Equals(dismissed, latestVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static void DismissBanner(VideoHubSettings settings, string latestVersion)
    {
        settings.CatchUpUpdateDismissedVersion = latestVersion.Trim();
        VideoHubSettingsStore.Save(settings);
    }
}
