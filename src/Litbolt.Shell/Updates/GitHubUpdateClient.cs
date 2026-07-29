using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Litbolt.Shell.Updates;

public sealed record UpdateCheckResult(
    bool Success,
    string? LatestVersion,
    string? ReleasePageUrl,
    string? ErrorMessage)
{
    public bool IsNewerThanCurrent { get; init; }
    public bool NoPublishedReleases { get; init; }
    public string? InstallerDownloadUrl { get; init; }
}

/// <summary>
/// WAID-style GitHub Releases check: paginated max tag, optional Setup-*.exe asset, no silent install.
/// </summary>
public sealed class GitHubUpdateClient
{
    private readonly string _gitHubRepo;
    private readonly string _installerPrefix;
    private readonly string _userAgent;
    private readonly HttpClient _http;

    public GitHubUpdateClient(string gitHubRepo, string installerPrefix, string userAgent)
    {
        _gitHubRepo = gitHubRepo;
        _installerPrefix = installerPrefix;
        _userAgent = userAgent;
        _http = CreateClient(userAgent);
    }

    public string ReleasesPageUrl => $"https://github.com/{_gitHubRepo}/releases";

    public static Version CurrentAssemblyVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version
        ?? Assembly.GetExecutingAssembly().GetName().Version
        ?? new Version(0, 0, 0);

    public async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        const int perPage = 100;
        try
        {
            Version? best = null;
            string? installerUrl = null;
            var sawAnyPage = false;

            for (var page = 1; page <= 5; page++)
            {
                var url = $"https://api.github.com/repos/{_gitHubRepo}/releases?per_page={perPage}&page={page}";
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new UpdateCheckResult(false, null, null,
                        "GitHub returned 404 — check repository name or network.");
                }

                if (!resp.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult(false, null, null,
                        $"GitHub returned {(int)resp.StatusCode} ({resp.ReasonPhrase}).");
                }

                var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement;
                if (arr.ValueKind != JsonValueKind.Array)
                    return new UpdateCheckResult(false, null, null, "Unexpected GitHub releases response.");

                sawAnyPage = true;
                if (arr.GetArrayLength() == 0)
                    break;

                foreach (var rel in arr.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True &&
                        draft.GetBoolean())
                        continue;

                    if (!rel.TryGetProperty("tag_name", out var tagEl))
                        continue;
                    var tag = tagEl.GetString()?.Trim() ?? "";
                    if (tag.StartsWith('v') || tag.StartsWith('V'))
                        tag = tag[1..];
                    if (!Version.TryParse(tag, out var v))
                        continue;

                    if (best is null || v > best)
                    {
                        best = v;
                        installerUrl = FindSetupInstallerUrl(rel);
                    }
                }

                if (arr.GetArrayLength() < perPage)
                    break;
            }

            if (!sawAnyPage || best is null)
            {
                return new UpdateCheckResult(true, null, ReleasesPageUrl, null)
                {
                    NoPublishedReleases = true,
                    IsNewerThanCurrent = false,
                };
            }

            var cur = CurrentAssemblyVersion;
            var newer = best > cur;
            return new UpdateCheckResult(true, best.ToString(3), ReleasesPageUrl, null)
            {
                IsNewerThanCurrent = newer,
                InstallerDownloadUrl = installerUrl,
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, null, ex.Message);
        }
    }

    private string? FindSetupInstallerUrl(JsonElement releaseRoot)
    {
        if (!releaseRoot.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameEl))
                continue;
            var name = nameEl.GetString() ?? "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!name.StartsWith(_installerPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (asset.TryGetProperty("browser_download_url", out var urlEl))
            {
                var u = urlEl.GetString();
                if (!string.IsNullOrEmpty(u))
                    return u;
            }
        }

        return null;
    }

    public void OpenUpdateDownload(string? installerDownloadUrl)
    {
        var url = !string.IsNullOrWhiteSpace(installerDownloadUrl)
            ? installerDownloadUrl
            : ReleasesPageUrl;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            OpenReleasesInBrowser();
        }
    }

    public void OpenReleasesInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleasesPageUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            /* ignore */
        }
    }

    public async Task<(string? FilePath, string? Error)> DownloadInstallerToTempAsync(
        string browserDownloadUrl,
        string? versionDisplay,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(browserDownloadUrl))
            return (null, "No download URL.");

        var label = string.IsNullOrWhiteSpace(versionDisplay)
            ? "latest"
            : string.Join("-", versionDisplay.Trim().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (label.Length > 48)
            label = label[..48];

        var path = Path.Combine(Path.GetTempPath(), $"{_installerPrefix}{label}.exe");
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            path = Path.Combine(Path.GetTempPath(), $"{_installerPrefix}{label}-{Guid.NewGuid():N}.exe");
        }

        using var dl = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        dl.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{_userAgent}-InstallerDownload");
        dl.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/octet-stream");

        try
        {
            using var resp = await dl
                .GetAsync(browserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (null, $"Download failed ({(int)resp.StatusCode} {resp.ReasonPhrase}).");

            await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536,
                         FileOptions.Asynchronous))
            {
                await resp.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(path))
                return (null, "Download finished but the file is missing.");

            var len = new FileInfo(path).Length;
            if (len < 64 * 1024)
            {
                try { File.Delete(path); } catch { /* ignore */ }
                return (null, "Downloaded file was too small — try the Releases page in your browser.");
            }

            return (path, null);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
            return (null, ex.Message);
        }
    }

    private static HttpClient CreateClient(string userAgent)
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        return c;
    }
}

public static class UpdateAvailabilityCache
{
    public static string? PendingVersion { get; private set; }
    public static string? InstallerDownloadUrl { get; private set; }

    public static bool HasPending => !string.IsNullOrWhiteSpace(PendingVersion);

    public static void Set(string? version, string? installerDownloadUrl)
    {
        PendingVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        InstallerDownloadUrl = installerDownloadUrl;
    }

    public static void Clear()
    {
        PendingVersion = null;
        InstallerDownloadUrl = null;
    }
}
