namespace LitboltMediaSession;

internal sealed class PendingSkip
{
    public string Verb = "";
    public long Tick;
    public DateTime Started;
    public DateTime LastSend;
    public string Baseline = "";
    public bool Sent;
}

internal static class TransportPending
{
    private static readonly Queue<PendingSkip> Skips = new();
    private static bool PlayOutstanding;
    private static bool PlaySent;
    private static bool PlayNeedsLive;
    private static int PlayFlips;
    private static bool PlayRecover;
    private static int PlayRecoverRaw;
    private static DateTime PlayUntil;
    private static DateTime PlayLastSend;
    private static long PlayTick;
    private static string PlayAction = "";
    private static long SeekMs;
    private static bool SeekOutstanding;
    private static bool SeekSent;
    private static DateTime SeekUntil;
    private static DateTime SeekLastSend;
    private static long SeekTick;

    internal static volatile bool NetworkRecovered;

    internal static void Record(string body, long tick)
    {
        if (body.StartsWith("seek ", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(body[5..].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seekMs))
        {
            SeekMs = Math.Max(0, seekMs);
            SeekOutstanding = true;
            SeekSent = false;
            SeekUntil = DateTime.UtcNow.AddSeconds(15);
            SeekTick = tick;
            return;
        }

        var space = body.IndexOf(' ');
        var verb = (space < 0 ? body : body[..space]).ToLowerInvariant();
        if (verb is "next" or "previous" or "prev")
        {
            Skips.Enqueue(new PendingSkip
            {
                Verb = verb is "prev" ? "previous" : verb,
                Tick = tick,
            });
            return;
        }

        if (verb != "playpause")
            return;

        var idle = !PlayOutstanding;
        PlayOutstanding = true;
        PlaySent = false;
        PlayRecover = false;
        PlayUntil = DateTime.UtcNow.AddSeconds(15);
        PlayTick = tick;
        if (idle || PlayNeedsLive)
        {
            // why: a stored pause from an earlier app-side change made the first click send Play
            PlayNeedsLive = true;
            PlayFlips = idle ? 1 : PlayFlips + 1;
            return;
        }

        ApplyFlip();
    }

    internal static void NoteExternalStatus(int raw)
    {
        if (raw is not (4 or 5))
            return;
        if (PlayOutstanding || PlayNeedsLive)
            return;
        PlayRecover = false;
        MediaState.IntendedPlaying = raw == 4;
    }

    private static void ApplyFlip()
    {
        var wantPlay = MediaState.IntendedPlaying != true;
        MediaState.IntendedPlaying = wantPlay;
        MediaState.OptimisticUntilUtc = DateTime.UtcNow.AddSeconds(15);
        PlayUntil = MediaState.OptimisticUntilUtc;
        PlayAction = wantPlay ? "Play" : "Pause";
        if (!wantPlay)
            HoldPause();
        else
        {
            MediaState.LastPlaybackStateForPoll = 1;
            MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
        }
    }

    internal static async Task PumpAsync(string dir)
    {
        await PumpPlayAsync(dir);
        await PumpSeekAsync(dir);
        await PumpSkipAsync(dir);
    }

    private static async Task PumpPlayAsync(string dir)
    {
        if (!PlayOutstanding && PlayRecover)
        {
            var rawNow = await SpotifyPlaybackCoordinator.ReadSpotifyRawAsync();
            var want = MediaState.IntendedPlaying == true;
            if (want ? rawNow == 4 : rawNow == 5)
            {
                PlayRecover = false;
                NetworkRecovered = false;
                MediaState.OptimisticUntilUtc = default;
                return;
            }

            var recovered = NetworkRecovered || (rawNow is 4 or 5 && rawNow != PlayRecoverRaw);
            if (!recovered)
                return;
            NetworkRecovered = false;
            PlayOutstanding = true;
            PlaySent = false;
            PlayUntil = DateTime.UtcNow.AddSeconds(15);
            MediaState.OptimisticUntilUtc = PlayUntil;
            PlayTick = System.Diagnostics.Stopwatch.GetTimestamp();
            PlayAction = want ? "Play" : "Pause";
            if (!want)
                HoldPause();
        }

        if (!PlayOutstanding)
            return;

        if (!PlaySent)
        {
            if (PlayNeedsLive)
            {
                var raw = await SpotifyPlaybackCoordinator.ReadSpotifyRawAsync();
                var playing = raw == 4 || (raw != 5 && MediaState.LastWrittenPlaybackState != 2);
                var flips = Math.Max(1, PlayFlips);
                for (var i = 0; i < flips; i++)
                    playing = !playing;
                MediaState.IntendedPlaying = playing;
                PlayNeedsLive = false;
                PlayFlips = 0;
                PlayAction = playing ? "Play" : "Pause";
                MediaState.OptimisticUntilUtc = DateTime.UtcNow.AddSeconds(15);
                PlayUntil = MediaState.OptimisticUntilUtc;
                if (!playing)
                    HoldPause();
                else
                {
                    MediaState.LastPlaybackStateForPoll = 1;
                    MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
                }
            }

            CommandLog.Write(dir, "send", PlayAction + (PlayRecover ? " recover " : " ") + Since(PlayTick));
            await SpotifyPlaybackCoordinator.SetExplicitPlayStateAsync(PlayAction == "Play");
            PlaySent = true;
            PlayLastSend = DateTime.UtcNow;
            PlayRecover = false;
            return;
        }

        if ((DateTime.UtcNow - PlayLastSend).TotalMilliseconds < 500)
            return;

        var live = await SpotifyPlaybackCoordinator.ReadSpotifyRawAsync();
        var wantPlaying = PlayAction == "Play";
        if (wantPlaying ? live == 4 : live == 5)
        {
            CommandLog.Write(dir, "confirm", PlayAction + " ok " + Since(PlayTick));
            PlayOutstanding = false;
            PlayRecover = false;
            MediaState.OptimisticUntilUtc = default;
            return;
        }

        if (DateTime.UtcNow >= PlayUntil)
        {
            CommandLog.Write(dir, "confirm", PlayAction + " timeout " + Since(PlayTick));
            PlayOutstanding = false;
            PlayRecover = true;
            PlayRecoverRaw = live;
            MediaState.OptimisticUntilUtc = default;
            return;
        }

        CommandLog.Write(dir, "send", PlayAction + " retry " + Since(PlayTick));
        await SpotifyPlaybackCoordinator.SetExplicitPlayStateAsync(wantPlaying);
        PlayLastSend = DateTime.UtcNow;
    }

    private static async Task PumpSkipAsync(string dir)
    {
        if (Skips.Count == 0)
            return;

        var head = Skips.Peek();
        var label = head.Verb == "next" ? "Next" : "Previous";
        var desktop = await SpotifyPlaybackCoordinator.ReadSpotifyDesktopAsync();
        if (!head.Sent)
        {
            head.Started = DateTime.UtcNow;
            var blocked = SkipBlockReason(desktop);
            if (blocked is not null)
            {
                CommandLog.Write(dir, "confirm", label + " failed " + blocked);
                Skips.Dequeue();
                return;
            }

            head.Baseline = desktop.Title;
            SpotifyPlaybackCoordinator.NoteLocalSkip();
            CommandLog.Write(dir, "send", label + " " + Since(head.Tick));
            await SpotifyPlaybackCoordinator.TransportViaSpotifySessionAsync(head.Verb);
            head.Sent = true;
            head.LastSend = DateTime.UtcNow;
            return;
        }

        if ((DateTime.UtcNow - head.LastSend).TotalMilliseconds < 400)
            return;

        var title = desktop.Title;
        if (!string.IsNullOrWhiteSpace(title)
            && !string.Equals(title, head.Baseline, StringComparison.Ordinal))
        {
            CommandLog.Write(dir, "confirm", label + " ok " + title + " " + Since(head.Tick));
            Skips.Dequeue();
            MediaState.SpotifyGsmtcObservedTitle = title;
            NetworkRecovered = true;
            return;
        }

        var why = string.IsNullOrWhiteSpace(title) ? "blank title" : "unchanged";
        CommandLog.Write(dir, "confirm", label + " failed " + why);
        Skips.Dequeue();
    }

    private static string? SkipBlockReason(SpotifyPlaybackCoordinator.SpotifyDesktop desktop)
    {
        var webapiPaused = string.Equals(MediaState.CommittedPlaybackSource, "webapi", StringComparison.OrdinalIgnoreCase)
            && MediaState.LastWrittenPlaybackState == 2;
        if (!desktop.Present)
            return webapiPaused ? "paused webapi" : "no Spotify desktop";
        if (string.IsNullOrWhiteSpace(desktop.Title))
            return "blank title";
        if (desktop.Raw != 4)
            return webapiPaused ? "paused webapi" : "paused";
        return null;
    }

    private static async Task PumpSeekAsync(string dir)
    {
        if (!SeekOutstanding)
            return;
        if (!SeekSent)
        {
            CommandLog.Write(dir, "send", "Seek " + SeekMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Since(SeekTick));
            await SpotifyPlaybackCoordinator.SeekViaSpotifySessionAsync(SeekMs);
            SeekSent = true;
            SeekLastSend = DateTime.UtcNow;
            return;
        }

        if ((DateTime.UtcNow - SeekLastSend).TotalMilliseconds < 500)
            return;

        var pos = SpotifyPlaybackCoordinator.CurrentTimelinePositionMs();
        if (Math.Abs(pos - SeekMs) < 1500)
        {
            CommandLog.Write(dir, "confirm", "Seek ok " + Since(SeekTick));
            SeekOutstanding = false;
            return;
        }

        if (DateTime.UtcNow >= SeekUntil)
        {
            CommandLog.Write(dir, "confirm", "Seek timeout " + Since(SeekTick));
            SeekOutstanding = false;
            return;
        }

        CommandLog.Write(dir, "send", "Seek retry " + Since(SeekTick));
        await SpotifyPlaybackCoordinator.SeekViaSpotifySessionAsync(SeekMs);
        SeekLastSend = DateTime.UtcNow;
    }

    private static void HoldPause()
    {
        var displayed = Math.Max(MediaState.LastWrittenPositionMs, SpotifyPlaybackCoordinator.CurrentTimelinePositionMs());
        var reported = MediaState.SpotifyTimelinePositionMs;
        var frozen = reported + 2000 < displayed ? reported : Math.Max(displayed, reported);
        MediaState.SpotifyTimelinePositionMs = Math.Max(0, frozen);
        MediaState.SpotifyTimelineUpdatedUtc = DateTimeOffset.UtcNow;
        MediaState.SpotifyGsmtcPositionMs = MediaState.SpotifyTimelinePositionMs;
        MediaState.LastSpotifyProgressMs = MediaState.SpotifyTimelinePositionMs;
        MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
        MediaState.LastPlaybackStateForPoll = 2;
    }

    private static string Since(long tick) =>
        "+" + System.Diagnostics.Stopwatch.GetElapsedTime(tick).TotalMilliseconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "ms";
}
