using System.Globalization;

namespace LitboltMediaSession;

internal static class NowPlayingIniWriter
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task WriteAsync(
        string statePath,
        string title,
        string artist,
        int state,
        double position,
        double duration,
        double progress,
        long coverRevision,
        string coverFile,
        string source,
        bool seekEnabled,
        long positionMs,
        long durationMs,
        DateTime pollUtc,
        string statusLine)
    {
        await Gate.WaitAsync();
        try
        {
        if (string.IsNullOrWhiteSpace(title)
            && string.IsNullOrWhiteSpace(statusLine)
            && !string.IsNullOrWhiteSpace(MediaState.LastWrittenTitle))
        {
            title = MediaState.LastWrittenTitle!;
            artist = MediaState.LastWrittenArtist ?? artist;
        }

        var dir = Path.GetDirectoryName(statePath) ?? "";
        ProgramHelpers.EnsureCoverExists(dir);
        if (!string.IsNullOrEmpty(MediaState.SkipBaselineTitle)
            && string.Equals(title, MediaState.SkipBaselineTitle, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(MediaState.LastWrittenTitle)
            && !string.Equals(MediaState.LastWrittenTitle, MediaState.SkipBaselineTitle, StringComparison.Ordinal))
        {
            // why: a poll that started on the previous song must not step the title back
            title = MediaState.AuthoritativeTitle ?? MediaState.LastWrittenTitle!;
            artist = MediaState.AuthoritativeArtist ?? MediaState.LastWrittenArtist ?? artist;
            if (MediaState.PublishedCoverRev > 0 && !string.IsNullOrEmpty(MediaState.PublishedCoverFile))
            {
                coverFile = MediaState.PublishedCoverFile!;
                coverRevision = MediaState.PublishedCoverRev;
            }
        }
        var titleKey = CoverArt.TrackKey(title, artist);
        var bound = MediaState.CoverBoundKey;
        var fileIsForTitle = string.IsNullOrEmpty(bound) || string.Equals(bound, titleKey, StringComparison.Ordinal);
        if (!fileIsForTitle
            && string.Equals(MediaState.PublishedCoverKey, titleKey, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(MediaState.PublishedCoverFile))
        {
            // why: a late fetch for the previous song must not replace the art already shown
            coverFile = MediaState.PublishedCoverFile!;
            coverRevision = Math.Max(coverRevision, MediaState.PublishedCoverRev);
        }

        if (coverRevision < MediaState.PublishedCoverRev && !string.IsNullOrEmpty(MediaState.PublishedCoverFile))
        {
            coverRevision = MediaState.PublishedCoverRev;
            coverFile = MediaState.PublishedCoverFile!;
        }

        if (string.IsNullOrEmpty(coverFile) || !File.Exists(Path.Combine(dir, coverFile)))
            coverFile = MediaState.CoverFileName;
        if (!string.IsNullOrEmpty(coverFile) && File.Exists(Path.Combine(dir, coverFile)))
        {
            var dash = coverFile.LastIndexOf('-');
            var dot = coverFile.LastIndexOf('.');
            if (dash >= 0 && dot > dash && long.TryParse(coverFile[(dash + 1)..dot], out var fromName))
                coverRevision = Math.Max(coverRevision, fromName);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Media]");
        sb.AppendLine($"Title={ProgramHelpers.EscapeIni(title)}");
        sb.AppendLine($"Artist={ProgramHelpers.EscapeIni(artist)}");
        sb.AppendLine($"Source={ProgramHelpers.EscapeIni(source)}");
        sb.AppendLine($"State={state}");
        sb.AppendLine($"Position={position.ToString("0.###", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Duration={duration.ToString("0.###", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"Progress={progress.ToString("0.##", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"PositionText={ProgramHelpers.FormatTime(position)}");
        sb.AppendLine($"DurationText={ProgramHelpers.FormatTime(duration)}");
        sb.AppendLine($"PositionMs={positionMs.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"DurationMs={durationMs.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"PollUtc={pollUtc.ToString("o", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"PollEpochMs={new DateTimeOffset(pollUtc).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}");
        MediaState.IniWriteSeq++;
        sb.AppendLine($"WriteSeq={MediaState.IniWriteSeq.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"SeekEnabled={(seekEnabled ? 1 : 0)}");
        sb.AppendLine($"StatusLine={ProgramHelpers.EscapeIni(statusLine)}");
        sb.AppendLine($"CoverRev={coverRevision}");
        sb.AppendLine($"CoverFile={ProgramHelpers.EscapeIni(coverFile)}");
        sb.AppendLine($"OfflineGsmtc={(MediaState.OfflineGsmtc ? 1 : 0)}");
        await AtomicFileWrite.WriteAllTextAsync(statePath, sb.ToString());
        if (coverRevision >= MediaState.PublishedCoverRev)
        {
            MediaState.PublishedCoverRev = coverRevision;
            MediaState.PublishedCoverFile = coverFile;
            if (fileIsForTitle)
                MediaState.PublishedCoverKey = titleKey;
        }
        if (!string.IsNullOrEmpty(coverFile) && File.Exists(Path.Combine(dir, coverFile)))
            ProgramHelpers.PruneOldCovers(dir);
        MediaState.LastStateWriteUtc = DateTime.UtcNow;
        MediaState.LastWrittenTitle = title;
        MediaState.LastWrittenArtist = artist;
        MediaState.LastWrittenDurationMs = durationMs;
        MediaState.LastWrittenPlaybackState = state;
        MediaState.LastWrittenPositionSec = position;
        MediaState.LastWrittenDurationSec = duration;
        MediaState.LastWrittenProgress = progress;
        MediaState.LastWrittenPositionMs = positionMs;
        MediaState.ForceNextStateWrite = false;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string? ReadIniValue(string path, string key)
    {
        if (!File.Exists(path))
            return null;
        var prefix = key + "=";
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line[prefix.Length..].Trim();
        }

        return null;
    }
}
