using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LitboltAppFolder.Services;

public sealed record PlaylistSummary(string Name, int TrackCount)
{
    public string Display => TrackCount > 0 ? $"{Name} ({TrackCount} tracks)" : Name;
}

public sealed record MusicNowPlaying(
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    bool IsPaused,
    DateTime? UpdatedUtc);

public static class MusicHubBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalMusicHub");

    public static string DatabasePath => Path.Combine(DataDirectory, "library.db");

    public static string PlaylistRequestPath => Path.Combine(DataDirectory, "playlist-request.json");
    public static string VolumeRequestPath => Path.Combine(DataDirectory, "volume-request.json");
    public static string NowPlayingPath => Path.Combine(DataDirectory, "now-playing.json");

    public static MusicNowPlaying? TryReadNowPlaying()
    {
        try
        {
            if (!File.Exists(NowPlayingPath))
                return null;

            var json = File.ReadAllText(NowPlayingPath);
            var dto = JsonSerializer.Deserialize<NowPlayingDto>(json, JsonOptions);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Title))
                return null;

            DateTime? updated = null;
            if (!string.IsNullOrWhiteSpace(dto.UpdatedUtc) &&
                DateTime.TryParse(dto.UpdatedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
                updated = parsed;

            return new MusicNowPlaying(
                dto.Title.Trim(),
                dto.Artist?.Trim() ?? "",
                dto.Album?.Trim() ?? "",
                dto.IsPlaying,
                dto.IsPaused,
                updated);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<string> ReadPlaylistNames() =>
        ReadPlaylistSummaries().Select(p => p.Name).ToList();

    public static IReadOnlyList<PlaylistSummary> ReadPlaylistSummaries()
    {
        if (!File.Exists(DatabasePath))
            return [];

        try
        {
            using var conn = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT p.name, COUNT(pt.track_id)
                FROM playlists p
                LEFT JOIN playlist_tracks pt ON pt.playlist_id = p.id
                GROUP BY p.id
                ORDER BY p.name COLLATE NOCASE
                """;
            var list = new List<PlaylistSummary>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                list.Add(new PlaylistSummary(name, reader.IsDBNull(1) ? 0 : reader.GetInt32(1)));
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    public static bool IsMusicHubRunning()
    {
        try
        {
            return Process.GetProcessesByName("LocalMusicHub").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void RequestPlayPlaylist(string playlistName, string? exePath)
    {
        if (string.IsNullOrWhiteSpace(playlistName))
            return;

        WritePlaylistRequest(playlistName);
        Launch(exePath, $"--playlist \"{EscapeArg(playlistName)}\"");
    }

    public static void RequestVolume(double volume, string? exePath)
    {
        volume = Clamp01(volume);
        WriteVolumeRequest(volume);
        Launch(exePath, $"--volume {volume.ToString(CultureInfo.InvariantCulture)}");
    }

    public static void LaunchStandalone(string? exePath, string extraArgs = "")
    {
        Launch(exePath, extraArgs);
    }

    private static void Launch(string? exePath, string args)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
            });
        }
        catch
        {
            /* ignore */
        }
    }

    private static void WritePlaylistRequest(string playlistName)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var payload = new { PlaylistName = playlistName.Trim(), RequestedUtc = DateTime.UtcNow.ToString("o") };
            File.WriteAllText(PlaylistRequestPath, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch
        {
            /* ignore */
        }
    }

    private static void WriteVolumeRequest(double volume)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var payload = new { Volume = volume, RequestedUtc = DateTime.UtcNow.ToString("o") };
            File.WriteAllText(VolumeRequestPath, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch
        {
            /* ignore */
        }
    }

    private static double Clamp01(double volume)
    {
        if (volume > 1.0 && volume <= 100.0)
            volume /= 100.0;
        return Math.Clamp(volume, 0, 1);
    }

    private static string EscapeArg(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed class NowPlayingDto
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public string? UpdatedUtc { get; set; }
    }
}
