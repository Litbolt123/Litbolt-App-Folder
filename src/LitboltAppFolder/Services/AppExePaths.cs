using System.IO;
using Litbolt.Shell;
using LitboltAppFolder.Services;

namespace LitboltAppFolder.Services;

public static class AppExePaths
{
    public static string DefaultMusicHubExe() =>
        Path.Combine(AppPathResolver.LocalAppPrograms, "LocalMusicHub", "LocalMusicHub.exe");

    public static string DefaultDownloaderExe() =>
        Path.Combine(AppPathResolver.LocalAppPrograms, "YouTubeToMp3", "YouTubeToMp3.exe");

    public static string DefaultVideoHubExe() =>
        Path.Combine(AppPathResolver.LocalAppPrograms, "LocalVideoHub", "LocalVideoHub.exe");

    public static string? ResolveMusicHub(FolderSettings settings) =>
        FirstExisting(
            settings.MusicHubExePath,
            DefaultMusicHubExe(),
            AppPathResolver.FindBesideOrInstalled("LocalMusicHub", "LocalMusicHub.exe", DefaultMusicHubExe()));

    public static string? ResolveDownloader(FolderSettings settings) =>
        FirstExisting(
            settings.DownloaderExePath,
            DefaultDownloaderExe(),
            AppPathResolver.FindBesideOrInstalled(
                "YouTubeToMp3",
                "YouTubeToMp3.exe",
                DefaultDownloaderExe(),
                AppPathResolver.FindInstalledExe("YouTube Downloader", "YouTubeToMp3.exe") ?? ""));

    public static string? ResolveVideoHub(FolderSettings settings) =>
        FirstExisting(
            settings.VideoHubExePath,
            DefaultVideoHubExe(),
            AppPathResolver.FindBesideOrInstalled(
                "LocalVideoHub",
                "LocalVideoHub.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LocalVideoHub.exe"),
                AppPathResolver.FindInstalledExe("LitboltVideo", "LitboltVideo.exe") ?? ""));

    private static string? FirstExisting(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return c;
        }

        return null;
    }
}
