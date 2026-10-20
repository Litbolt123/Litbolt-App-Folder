using Windows.Media.Control;
using Windows.Storage.Streams;

namespace LitboltMediaSession;

internal enum SpotifyPollOutcome
{
    DisplaySpotify,
    HoldSpotifyDisplay,
    FallThrough,
}

internal static class SpotifyPlaybackCoordinator
{
    private const int SwitchThreshold = 2;

    public static async Task PollAndWriteAsync(string dir, CancellationToken ct = default)
    {
        if (!SpotifyTokenStore.HasRefreshToken())
            return;

        MediaState.ForceNextStateWrite = true;
        MediaStateStore.Load(dir);
        var outcome = await PollAsync(dir, ct);
        if (outcome == SpotifyPollOutcome.FallThrough)
            await WriteOfflineAsync(dir);
        MediaState.LastSpotifyPollAttemptUtc = DateTime.UtcNow;
        MediaStateStore.Save(dir);
    }

    public static async Task<SpotifyPollOutcome> PollAsync(string dir, CancellationToken ct = default)
    {
        if (!SpotifyTokenStore.HasRefreshToken())
            return SpotifyPollOutcome.FallThrough;

        if (DebugOffline.IsOn())
        {
            NoteApiReachability(false);
            MediaState.OfflineGsmtc = true;
            return SpotifyPollOutcome.FallThrough;
        }

        var started = DateTime.UtcNow;
        var generation = CoverFast.CurrentGeneration;
        var (player, status, retryAfter) = await SpotifyApiClient.GetPlayerAsync(ct);
        var reachable = status is SpotifyPlayerStatus.Ok or SpotifyPlayerStatus.NoContent or SpotifyPlayerStatus.RateLimited;
        NoteApiReachability(reachable);

        if (MediaState.OfflineGsmtc)
            return SpotifyPollOutcome.FallThrough;

        if (status == SpotifyPlayerStatus.Unauthorized)
        {
            await WriteSpotifyStatusAsync(dir, "Spotify sign-in expired. Right-click the skin and choose Connect Spotify.");
            return SpotifyPollOutcome.DisplaySpotify;
        }

        if (status == SpotifyPlayerStatus.RateLimited)
        {
            MediaState.SpotifyBackoffUntilUtc = DateTime.UtcNow.Add(retryAfter ?? TimeSpan.FromSeconds(5));
            return SpotifyPollOutcome.DisplaySpotify;
        }

        if (status != SpotifyPlayerStatus.Ok || player is null || string.IsNullOrWhiteSpace(player.Title))
            return SpotifyPollOutcome.DisplaySpotify;

        if (!AcceptWebPlayer(started, generation, player))
            return SpotifyPollOutcome.DisplaySpotify;

        MediaState.CommittedPlaybackSource = "webapi";
        MediaState.UseSpotifyTransport = true;
        await WriteActivePlayerAsync(dir, player, ct);
        return SpotifyPollOutcome.DisplaySpotify;
    }

    internal static void NoteLocalSkip()
    {
        MediaState.SkipBaselineTitle = MediaState.SpotifyGsmtcObservedTitle ?? MediaState.LastTitle;
        MediaState.TrackChangedUtc = DateTime.UtcNow;
        MediaState.AuthoritativeTitle = null;
        MediaState.AuthoritativeArtist = null;
        MediaState.AuthoritativeTrackId = null;
        MediaState.HoldWebUntilGsmtc = true;
        CoverFast.BumpGeneration();
    }

    private static bool IsBaselineRegression(string? title)
    {
        return !string.IsNullOrEmpty(MediaState.SkipBaselineTitle)
            && string.Equals(title, MediaState.SkipBaselineTitle, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(MediaState.LastWrittenTitle)
            && !string.Equals(MediaState.LastWrittenTitle, MediaState.SkipBaselineTitle, StringComparison.Ordinal);
    }

    private static bool AcceptWebPlayer(DateTime started, int generation, SpotifyPlayerSnapshot player)
    {
        if (MediaState.HoldWebUntilGsmtc
            && MediaState.TrackChangedUtc != default
            && DateTime.UtcNow - MediaState.TrackChangedUtc > TimeSpan.FromSeconds(3)
            && string.IsNullOrEmpty(MediaState.AuthoritativeTitle))
            MediaState.HoldWebUntilGsmtc = false;

        if (MediaState.TrackChangedUtc != default && started < MediaState.TrackChangedUtc)
            return false;
        if (generation != CoverFast.CurrentGeneration)
            return false;
        if (MediaState.HoldWebUntilGsmtc)
            return false;
        if (!string.IsNullOrEmpty(MediaState.AuthoritativeTrackId)
            && string.Equals(player.TrackId, MediaState.AuthoritativeTrackId, StringComparison.Ordinal))
            return true;
        if (IsBaselineRegression(player.Title))
            return false;
        if (!string.IsNullOrEmpty(MediaState.AuthoritativeTitle)
            && !string.Equals(player.Title, MediaState.AuthoritativeTitle, StringComparison.Ordinal))
            return false;
        if (!string.IsNullOrEmpty(MediaState.AuthoritativeTitle)
            && string.Equals(player.Title, MediaState.AuthoritativeTitle, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(player.TrackId))
            MediaState.AuthoritativeTrackId = player.TrackId;
        return true;
    }

    private static void NoteApiReachability(bool reachable)
    {
        if (reachable)
        {
            MediaState.WebApiFailStreak = 0;
            MediaState.WebApiFailSinceUtc = default;
            MediaState.WebApiOkStreak++;
            if (MediaState.OfflineGsmtc && MediaState.WebApiOkStreak >= 3)
                MediaState.OfflineGsmtc = false;
            return;
        }

        MediaState.WebApiOkStreak = 0;
        if (MediaState.WebApiFailStreak == 0)
            MediaState.WebApiFailSinceUtc = DateTime.UtcNow;
        MediaState.WebApiFailStreak++;
        var downFor = MediaState.WebApiFailSinceUtc == default
            ? TimeSpan.Zero
            : DateTime.UtcNow - MediaState.WebApiFailSinceUtc;
        var networkDown = DebugOffline.IsOn() || !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        if (!MediaState.OfflineGsmtc && (networkDown || downFor >= TimeSpan.FromSeconds(2)))
            MediaState.OfflineGsmtc = true;
    }

    public static bool ShouldBlockGsmtc() =>
        string.Equals(MediaState.CommittedPlaybackSource, "spotify", StringComparison.OrdinalIgnoreCase);

    public static async Task MaybeWritePlaybackHeartbeatAsync(string dir)
    {
        if (!ShouldBlockGsmtc())
            return;

        if (ShouldPollSpotifyNow())
            return;
    }

    public static bool ShouldPollSpotifyNow()
    {
        if (MediaState.LastSpotifyPollAttemptUtc == default)
            return true;

        var elapsed = (DateTime.UtcNow - MediaState.LastSpotifyPollAttemptUtc).TotalMilliseconds;
        var recovering = MediaState.OfflineGsmtc
            && !DebugOffline.IsOn()
            && System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        var interval = recovering
            ? 1500
            : MediaState.OfflineGsmtc
                ? 5000
                : MediaState.LastPlaybackStateForPoll == 1 ? 1000 : 3000;
        return elapsed >= interval;
    }

    private static int ProbeGate;
    private static CancellationTokenSource? ProbeCancel;

    public static void CancelProbe()
    {
        try { ProbeCancel?.Cancel(); } catch { /* already cancelled */ }
    }

    public static void KickProbe(string dir)
    {
        if (!SpotifyTokenStore.HasRefreshToken() || !ShouldPollSpotifyNow())
            return;
        if (Interlocked.CompareExchange(ref ProbeGate, 1, 0) != 0)
            return;

        MediaState.LastSpotifyPollAttemptUtc = DateTime.UtcNow;
        var probeCt = new CancellationTokenSource();
        ProbeCancel = probeCt;
        _ = Task.Run(async () =>
        {
            try
            {
                var outcome = await PollAsync(dir, probeCt.Token);
                if (outcome == SpotifyPollOutcome.FallThrough)
                    await WriteOfflineAsync(dir);
                MediaStateStore.Save(dir);
            }
            catch
            {
            }
            finally
            {
                probeCt.Dispose();
                Interlocked.Exchange(ref ProbeGate, 0);
            }
        });
    }

    public static int PublishedState(int playbackState)
    {
        if (MediaState.IntendedPlaying is bool want && DateTime.UtcNow < MediaState.OptimisticUntilUtc)
            return want ? 1 : 2;
        return playbackState;
    }

    public static async Task NoteSpotifyTrackChangeAsync()
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;

                HookSpotifyTrack(session);
                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                CaptureTimeline((int)playback.PlaybackStatus, timeline);
                var generation = CoverFast.CurrentGeneration;
                var props = await session.TryGetMediaPropertiesAsync();
                if (generation != CoverFast.CurrentGeneration)
                    return;
                var title = props.Title ?? "";
                if (IsBaselineRegression(title))
                    return;
                if (!string.IsNullOrEmpty(MediaState.SpotifyGsmtcObservedTitle)
                    && !string.Equals(title, MediaState.SpotifyGsmtcObservedTitle, StringComparison.Ordinal))
                {
                    MediaState.LastSpotifyPollAttemptUtc = default;
                    MediaState.AuthoritativeTitle = title;
                    MediaState.AuthoritativeArtist = props.Artist ?? "";
                    MediaState.AuthoritativeTrackId = null;
                    if (!string.Equals(title, MediaState.SkipBaselineTitle, StringComparison.Ordinal))
                        MediaState.HoldWebUntilGsmtc = false;
                }
                MediaState.SpotifyGsmtcObservedTitle = title;
                GsmtcArtist = props.Artist ?? "";
                return;
            }
        }
        catch
        {
        }
    }

    private static readonly HashSet<string> HookedSpotify = new(StringComparer.OrdinalIgnoreCase);
    private static string GsmtcArtist = "";
    private static int AppliedGsmtcRaw;

    private static void HookSpotifyTrack(GlobalSystemMediaTransportControlsSession session)
    {
        var id = session.SourceAppUserModelId ?? "";
        lock (HookedSpotify)
        {
            if (!HookedSpotify.Add(id))
                return;
        }

        session.PlaybackInfoChanged += (_, _) =>
        {
            try
            {
                TransportPending.NoteExternalStatus((int)session.GetPlaybackInfo().PlaybackStatus);
            }
            catch
            {
            }

            QueueSpotifyEvent();
        };
        session.TimelinePropertiesChanged += (_, _) => QueueSpotifyEvent();
        session.MediaPropertiesChanged += (_, _) =>
        {
            MediaState.LastSpotifyPollAttemptUtc = default;
            QueueSpotifyEvent();
        };
    }

    private static async Task WriteSpotifyStatusAsync(string dir, string statusLine)
    {
        MediaState.CommittedPlaybackSource = "webapi";
        await NowPlayingIniWriter.WriteAsync(
            Path.Combine(dir, "nowplaying.ini"),
            "",
            "",
            0,
            0,
            0,
            0,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            "webapi",
            true,
            0,
            0,
            DateTime.UtcNow,
            statusLine);
    }

    private static void ResetStreaks()
    {
        MediaState.SpotifyActiveStreak = 0;
        MediaState.SpotifyInactiveStreak = 0;
    }

    private static bool InferIsPlaying(SpotifyPlayerSnapshot player)
    {
        if (player.IsPlaying)
            return true;

        if (MediaState.LastSpotifyPollUtc == default)
            return false;

        var elapsed = (DateTime.UtcNow - MediaState.LastSpotifyPollUtc).TotalMilliseconds;
        if (elapsed > 4000)
            return false;

        return player.ProgressMs > MediaState.LastSpotifyProgressMs + 250;
    }

    private static async Task WriteActivePlayerAsync(string dir, SpotifyPlayerSnapshot player, CancellationToken ct)
    {
        if (IsBaselineRegression(player.Title))
            return;
        if (!string.IsNullOrEmpty(MediaState.AuthoritativeTitle)
            && !string.Equals(player.Title, MediaState.AuthoritativeTitle, StringComparison.Ordinal)
            && !string.Equals(player.TrackId, MediaState.AuthoritativeTrackId, StringComparison.Ordinal))
            return;

        var statePath = Path.Combine(dir, "nowplaying.ini");
        var playbackState = player.IsPlaying ? 1 : 2;
        var positionMs = Math.Max(0, player.ProgressMs);
        if (MediaState.SpotifyGsmtcRaw == 4)
        {
            playbackState = 1;
            var live = CurrentTimelinePositionMs();
            if (live > 0)
                positionMs = live;
        }
        else if (MediaState.SpotifyGsmtcRaw == 5)
        {
            var frozen = CurrentTimelinePositionMs();
            // why: the Web API progress lags the speakers by about 2s when Spotify pauses
            if (frozen > positionMs && frozen - positionMs < 4000)
                positionMs = frozen;
        }

        var durationMs = player.DurationMs;
        if (MediaState.SpotifyGsmtcRaw == 4 && MediaState.SpotifyGsmtcDurationMs > durationMs)
            durationMs = MediaState.SpotifyGsmtcDurationMs;
        if (durationMs > 0)
            positionMs = Math.Min(positionMs, durationMs);

        var title = player.Title;
        var artist = player.Artist;
        var titleChanged = !string.Equals(MediaState.LastTitle, title, StringComparison.Ordinal);
        var trackIdChanged = !string.Equals(player.TrackId, MediaState.LastSpotifyTrackId, StringComparison.Ordinal);

        var sampleMoved = MediaState.LastSpotifyPollUtc == default
            || titleChanged
            || Math.Abs(positionMs - MediaState.LastSpotifyProgressMs) > 400;
        var writePos = positionMs;
        if (!sampleMoved && playbackState == 1 && MediaState.LastSpotifyPollUtc != default)
        {
            writePos = MediaState.LastSpotifyProgressMs
                + (long)(DateTime.UtcNow - MediaState.LastSpotifyPollUtc).TotalMilliseconds;
            if (durationMs > 0)
                writePos = Math.Min(writePos, durationMs);
        }
        else
        {
            MediaState.LastSpotifyProgressMs = positionMs;
            MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
        }

        var durationSec = durationMs / 1000.0;
        var positionSec = writePos / 1000.0;
        var progress = durationMs > 0
            ? Math.Clamp(writePos * 100.0 / durationMs, 0, 100)
            : 0;

        if (titleChanged)
        {
            MediaState.PreviousTitle = MediaState.LastTitle;
            MediaState.LastTitle = title;
            MediaState.LastArtist = artist;
            CoverArt.OnTrackIdentityChanged(title, artist);
            MediaState.ForceNextStateWrite = true;
            var generation = CoverFast.BumpGeneration();
            if (!CoverFast.TryReady(dir, title, artist, player.TrackId))
                await CoverFast.TryGsmtcAsync(dir, title, artist);
            CoverFast.BeginWebUpgrade(dir, title, artist, player.TrackId, player.CoverUrl, generation, true);
        }
        else
            CoverFast.BeginWebUpgrade(dir, title, artist, player.TrackId, player.CoverUrl, CoverFast.CurrentGeneration, false);

        if (!string.IsNullOrEmpty(player.TrackId))
            MediaState.LastSpotifyTrackId = player.TrackId;

        MediaState.LastSource = "webapi";
        MediaState.CommittedPlaybackSource = "webapi";
        MediaState.UseSpotifyTransport = true;
        MediaState.LastPlaybackStateForPoll = playbackState;

        await WriteSpotifyIniAsync(
            statePath,
            title,
            artist,
            PublishedState(playbackState),
            positionSec,
            durationSec,
            progress,
            writePos,
            durationMs);

        MediaState.LastWrittenPositionSec = positionSec;
        MediaState.LastWrittenDurationSec = durationSec;
        MediaState.LastWrittenProgress = progress;
        MediaState.LastWrittenPositionMs = writePos;
        MediaState.LastSpotifyProgressMs = writePos;
        MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
        CoverFast.BeginPrefetch(dir, titleChanged);

        MediaStateStore.Save(dir);
    }

    private static async Task WriteSpotifyIniAsync(
        string statePath,
        string title,
        string artist,
        int playbackState,
        double positionSec,
        double durationSec,
        double progress,
        long positionMs,
        long durationMs)
    {
        await NowPlayingIniWriter.WriteAsync(
            statePath,
            title,
            artist,
            PublishedState(playbackState),
            positionSec,
            durationSec,
            progress,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            "webapi",
            seekEnabled: true,
            positionMs: positionMs,
            durationMs: durationMs,
            pollUtc: DateTime.UtcNow,
            statusLine: "");
    }

    private static async Task TryUpdateCoverAsync(
        string dir,
        SpotifyPlayerSnapshot player,
        bool trackIdentityChanged,
        CancellationToken ct)
    {
        var generation = CoverFast.CurrentGeneration;
        var trackKey = CoverArt.TrackKey(player.Title, player.Artist);
        if (!CoverArt.IsCurrentTrack(trackKey, generation))
            return;
        if ((trackIdentityChanged || MediaState.LastCoverHash == 0)
            && CoverCache.TryApply(dir, player.Title, player.Artist))
            return;

        if (!string.IsNullOrEmpty(player.TrackId))
        {
            var cached = Path.Combine(dir, $"spotify-track-{player.TrackId}.jpg");
            if (File.Exists(cached))
            {
                var cachedBytes = await File.ReadAllBytesAsync(cached, ct);
                if (cachedBytes.Length > 0)
                {
                    await CoverArt.UpdateFromBytesAsync(
                        dir,
                        cachedBytes,
                        ProgramHelpers.ComputeCoverHash,
                        _ => ProgramHelpers.PruneOldCovers(dir),
                        trackIdentityChanged,
                        trackKey,
                        generation);
                    return;
                }
            }
        }

        var spotifyBytes = await SpotifyApiClient.DownloadCoverAsync(player.CoverUrl, ct);
        var bytes = spotifyBytes;
        if (bytes is null or { Length: 0 })
            bytes = await TryDownloadSpotifyGsmtcThumbAsync(ct);

        if (bytes is { Length: > 0 })
        {
            if (spotifyBytes is null && CoverArt.IsAppIcon(bytes))
                return;
            if (!CoverArt.IsCurrentTrack(trackKey, generation))
                return;

            var cacheName = MediaState.CoverFileName;
            if (!string.IsNullOrEmpty(player.TrackId))
            {
                cacheName = $"spotify-track-{player.TrackId}.jpg";
                await AtomicFileWrite.WriteAllBytesAsync(Path.Combine(dir, cacheName), bytes);
            }

            await CoverArt.UpdateFromBytesAsync(
                dir,
                bytes,
                ProgramHelpers.ComputeCoverHash,
                _ => ProgramHelpers.PruneOldCovers(dir),
                trackIdentityChanged,
                trackKey,
                generation);
            CoverCache.Remember(
                dir,
                player.Title,
                player.Artist,
                string.IsNullOrEmpty(player.TrackId) ? MediaState.CoverFileName : cacheName);
        }
    }

    private static async Task<byte[]?> TryDownloadSpotifyGsmtcThumbAsync(CancellationToken ct)
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;

                var props = await session.TryGetMediaPropertiesAsync();
                var thumb = props.Thumbnail;
                if (thumb is null)
                    continue;

                using var stream = await thumb.OpenReadAsync();
                var bytes = new byte[stream.Size];
                using var reader = new DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
                return bytes;
            }
        }
        catch
        {
        }

        return null;
    }

    public static async Task WriteOfflineAsync(string dir)
    {
        var title = MediaState.SpotifyGsmtcObservedTitle ?? "";
        if (string.IsNullOrWhiteSpace(title))
            return;

        var raw = MediaState.SpotifyGsmtcRaw;
        var playbackState = raw == 4 ? 1 : raw == 5 || raw == 2 ? 2 : 0;
        var durationMs = Math.Max(0, MediaState.SpotifyGsmtcDurationMs);
        var positionMs = CurrentTimelinePositionMs();
        if (durationMs > 0)
            positionMs = Math.Min(positionMs, durationMs);
        var durationSec = durationMs / 1000.0;
        var positionSec = positionMs / 1000.0;
        var progress = durationMs > 0 ? Math.Clamp(positionMs * 100.0 / durationMs, 0, 100) : 0;
        var artist = GsmtcArtist;
        if (IsBaselineRegression(title))
        {
            title = MediaState.AuthoritativeTitle ?? MediaState.LastWrittenTitle ?? title;
            artist = MediaState.AuthoritativeArtist ?? artist;
        }
        var titleChanged = !string.Equals(MediaState.LastTitle, title, StringComparison.Ordinal);

        if (titleChanged)
        {
            MediaState.PreviousTitle = MediaState.LastTitle;
            MediaState.LastTitle = title;
            MediaState.LastArtist = artist;
            CoverArt.OnTrackIdentityChanged(title, artist);
            MediaState.LastSpotifyPollUtc = default;
        }

        await EnsureGsmtcCoverAsync(dir, title, artist, titleChanged);

        MediaState.LastSpotifyProgressMs = positionMs;
        MediaState.LastSpotifyPollUtc = DateTime.UtcNow;

        MediaState.LastSource = "gsmtc";
        MediaState.CommittedPlaybackSource = "gsmtc";
        MediaState.UseSpotifyTransport = false;
        MediaState.LastPlaybackStateForPoll = playbackState;
        await NowPlayingIniWriter.WriteAsync(
            Path.Combine(dir, "nowplaying.ini"),
            title,
            artist,
            PublishedState(playbackState),
            positionSec,
            durationSec,
            progress,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            "gsmtc",
            seekEnabled: false,
            positionMs: positionMs,
            durationMs: durationMs,
            pollUtc: DateTime.UtcNow,
            statusLine: "");
        MediaState.LastWrittenPositionSec = positionSec;
        MediaState.LastWrittenDurationSec = durationSec;
        MediaState.LastWrittenProgress = progress;
        MediaState.LastWrittenPositionMs = positionMs;
        MediaStateStore.Save(dir);
    }

    public static async Task ApplyLiveGsmtcPlayStateAsync(string dir)
    {
        if (MediaState.SpotifyGsmtcSampleUtc == default)
            return;
        if ((DateTime.UtcNow - MediaState.SpotifyGsmtcSampleUtc).TotalSeconds > 2)
            return;
        if (string.IsNullOrWhiteSpace(MediaState.LastWrittenTitle))
            return;

        var raw = MediaState.SpotifyGsmtcRaw;
        if (raw != 4 && raw != 5)
            return;
        if (raw == AppliedGsmtcRaw)
            return;

        var previous = AppliedGsmtcRaw;
        AppliedGsmtcRaw = raw;
        if (previous == 0 && raw != 4)
            return;
        if (raw != 4 && previous != 4)
            return;

        var playbackState = raw == 4 ? 1 : 2;
        if (playbackState == MediaState.LastWrittenPlaybackState)
            return;

        var durationMs = MediaState.SpotifyGsmtcDurationMs > 0
            ? MediaState.SpotifyGsmtcDurationMs
            : MediaState.LastWrittenDurationSec > 0 ? (long)(MediaState.LastWrittenDurationSec * 1000) : 0;
        var positionMs = CurrentTimelinePositionMs();
        if (durationMs > 0)
            positionMs = Math.Min(Math.Max(0, positionMs), durationMs);
        var durationSec = durationMs / 1000.0;
        var positionSec = positionMs / 1000.0;
        var progress = durationMs > 0 ? Math.Clamp(positionMs * 100.0 / durationMs, 0, 100) : MediaState.LastWrittenProgress;

        MediaState.LastPlaybackStateForPoll = playbackState;
        await NowPlayingIniWriter.WriteAsync(
            Path.Combine(dir, "nowplaying.ini"),
            MediaState.LastWrittenTitle ?? "",
            MediaState.LastWrittenArtist ?? "",
            PublishedState(playbackState),
            positionSec,
            durationSec,
            progress,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            "webapi",
            seekEnabled: true,
            positionMs: positionMs,
            durationMs: durationMs,
            pollUtc: DateTime.UtcNow,
            statusLine: "");
        MediaState.LastWrittenPositionSec = positionSec;
        MediaState.LastWrittenDurationSec = durationSec;
        MediaState.LastWrittenProgress = progress;
        MediaState.LastWrittenPositionMs = positionMs;
    }

    public static async Task<bool> TransportViaSpotifySessionAsync(string verb)
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (verb.ToLowerInvariant())
                {
                    case "play":
                        return await session.TryPlayAsync();
                    case "pause":
                        return await session.TryPauseAsync();
                    case "playpause":
                        return await session.TryTogglePlayPauseAsync();
                    case "next":
                        return await session.TrySkipNextAsync();
                    case "previous":
                    case "prev":
                        return await session.TrySkipPreviousAsync();
                    default:
                        return false;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public static async Task<int> ReadSpotifyRawAsync()
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;
                return (int)session.GetPlaybackInfo().PlaybackStatus;
            }
        }
        catch
        {
        }

        return 0;
    }

    public static async Task<string> ReadSpotifyTitleAsync()
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;
                var props = await session.TryGetMediaPropertiesAsync();
                return props.Title ?? "";
            }
        }
        catch
        {
        }

        return MediaState.SpotifyGsmtcObservedTitle ?? "";
    }

    public static async Task<bool> SetExplicitPlayStateAsync(bool play) =>
        await TransportViaSpotifySessionAsync(play ? "play" : "pause");

    public static async Task<bool> SeekViaSpotifySessionAsync(long positionMs)
    {
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in mgr.GetSessions())
            {
                var id = session.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;
                return await session.TryChangePlaybackPositionAsync(Math.Max(0, positionMs) * 10_000L);
            }
        }
        catch
        {
        }

        return false;
    }

    public static async Task WriteLiveClockAsync(string dir)
    {
        var title = MediaState.LastWrittenTitle;
        if (string.IsNullOrWhiteSpace(title))
            return;

        var state = MediaState.LastPlaybackStateForPoll;
        if (MediaState.SpotifyGsmtcRaw == 4)
            state = 1;
        state = PublishedState(state);

        var positionMs = MediaState.SpotifyTimelineUpdatedUtc != default
            ? CurrentTimelinePositionMs()
            : MediaState.LastSpotifyProgressMs > 0
                ? MediaState.LastSpotifyProgressMs
                : MediaState.LastWrittenPositionMs;
        if (MediaState.SpotifyTimelineUpdatedUtc == default && state == 1 && MediaState.LastSpotifyPollUtc != default)
            positionMs += (long)(DateTime.UtcNow - MediaState.LastSpotifyPollUtc).TotalMilliseconds;

        var durationMs = MediaState.LastWrittenDurationMs;
        if (durationMs <= 0 && MediaState.LastWrittenDurationSec > 0)
            durationMs = (long)(MediaState.LastWrittenDurationSec * 1000);
        if (durationMs > 0)
            positionMs = Math.Min(Math.Max(0, positionMs), durationMs);
        else
            positionMs = Math.Max(0, positionMs);

        var durationSec = durationMs / 1000.0;
        var positionSec = positionMs / 1000.0;
        var progress = durationMs > 0 ? Math.Clamp(positionMs * 100.0 / durationMs, 0, 100) : 0;
        await NowPlayingIniWriter.WriteAsync(
            Path.Combine(dir, "nowplaying.ini"),
            title,
            MediaState.LastWrittenArtist ?? "",
            state,
            positionSec,
            durationSec,
            progress,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            "webapi",
            seekEnabled: true,
            positionMs: positionMs,
            durationMs: durationMs,
            pollUtc: DateTime.UtcNow,
            statusLine: "");
    }

    private static async Task EnsureGsmtcCoverAsync(string dir, string title, string artist, bool titleChanged)
    {
        if (CoverFast.TryReady(dir, title, artist, null))
            return;

        var coverPath = Path.Combine(dir, MediaState.CoverFileName ?? "");
        var missing = MediaState.LastCoverHash == 0 || string.IsNullOrEmpty(MediaState.CoverFileName) || !File.Exists(coverPath);
        if (!titleChanged && !missing && MediaState.LastCoverHash != 0)
            return;
        var waitSec = MediaState.LastCoverHash == 0 ? 0.2 : 2;
        if (!titleChanged
            && MediaState.LastCoverRetryUtc != default
            && (DateTime.UtcNow - MediaState.LastCoverRetryUtc).TotalSeconds < waitSec)
            return;

        MediaState.LastCoverRetryUtc = DateTime.UtcNow;
        await CoverFast.TryGsmtcAsync(dir, title, artist);
    }

    public static void SetWatchDir(string dir) => WatchDir = dir;

    public static long CurrentTimelinePositionMs()
    {
        if (MediaState.IntendedPlaying == false && DateTime.UtcNow < MediaState.OptimisticUntilUtc)
            return Math.Max(0, MediaState.SpotifyTimelinePositionMs);

        if (MediaState.SpotifyTimelineUpdatedUtc == default)
            return Math.Max(0, MediaState.SpotifyGsmtcPositionMs);

        var pos = MediaState.SpotifyTimelinePositionMs;
        if (MediaState.SpotifyGsmtcRaw == 4)
        {
            pos += ClampAge(DateTimeOffset.UtcNow - MediaState.SpotifyTimelineUpdatedUtc);
            var end = MediaState.SpotifyGsmtcDurationMs;
            if (end > 0 && pos > end)
                pos = end;
        }

        return Math.Max(0, pos);
    }

    private static string WatchDir = "";
    private static int EventGate;
    private static int EventPending;

    private static void QueueSpotifyEvent()
    {
        if (string.IsNullOrEmpty(WatchDir))
            return;
        Interlocked.Exchange(ref EventPending, 1);
        if (Interlocked.CompareExchange(ref EventGate, 1, 0) != 0)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                while (Interlocked.Exchange(ref EventPending, 0) == 1 && !string.IsNullOrEmpty(WatchDir))
                    await AdoptSpotifyTimelineAsync(WatchDir);
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref EventGate, 0);
            }
        });
    }

    private static async Task AdoptSpotifyTimelineAsync(string dir)
    {
        GlobalSystemMediaTransportControlsSession? session = null;
        try
        {
            var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var candidate in mgr.GetSessions())
            {
                var id = candidate.SourceAppUserModelId ?? "";
                if (!id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    continue;
                session = candidate;
                HookSpotifyTrack(candidate);
                break;
            }
        }
        catch
        {
            return;
        }

        if (session is null)
            return;

        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var raw = (int)playback.PlaybackStatus;
        var previousRaw = MediaState.SpotifyGsmtcRaw;
        CaptureTimeline(raw, timeline);
        var stateChanged = (previousRaw == 4) != (raw == 4) && (raw == 4 || raw == 5);
        var positionMs = CurrentTimelinePositionMs();
        var seek = Math.Abs(positionMs - MediaState.LastWrittenPositionMs) > 2500;

        string title = MediaState.SpotifyGsmtcObservedTitle ?? MediaState.LastWrittenTitle ?? "";
        string artist = GsmtcArtist;
        if (IsBaselineRegression(title))
        {
            title = MediaState.AuthoritativeTitle ?? MediaState.LastWrittenTitle ?? title;
            artist = MediaState.AuthoritativeArtist ?? artist;
        }
        var titleChanged = false;
        var readGeneration = CoverFast.CurrentGeneration;
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var nextTitle = props.Title ?? "";
            var staleRead = readGeneration != CoverFast.CurrentGeneration;
            if (!staleRead && !IsBaselineRegression(nextTitle) && !string.IsNullOrWhiteSpace(nextTitle)
                && !string.Equals(nextTitle, title, StringComparison.Ordinal))
            {
                titleChanged = true;
                title = nextTitle;
                artist = props.Artist ?? artist;
                MediaState.SpotifyGsmtcObservedTitle = title;
                GsmtcArtist = artist;
                MediaState.AuthoritativeTitle = title;
                MediaState.AuthoritativeArtist = artist;
                MediaState.AuthoritativeTrackId = null;
                if (!string.Equals(title, MediaState.SkipBaselineTitle, StringComparison.Ordinal))
                    MediaState.HoldWebUntilGsmtc = false;
                CoverArt.OnTrackIdentityChanged(title, artist);
            }
        }
        catch
        {
        }

        if (!stateChanged && !seek && !titleChanged && MediaState.LastCoverHash != 0)
            return;
        if (string.IsNullOrWhiteSpace(title))
            return;

        var playbackState = raw == 4 ? 1 : 2;
        var durationMs = Math.Max(0, MediaState.SpotifyGsmtcDurationMs);
        if (durationMs > 0)
            positionMs = Math.Min(positionMs, durationMs);
        MediaState.GsmtcAnchorUtc = DateTime.UtcNow;
        MediaState.LastSpotifyProgressMs = positionMs;
        MediaState.LastSpotifyPollUtc = DateTime.UtcNow;
        MediaState.LastPlaybackStateForPoll = playbackState;
        if (titleChanged)
        {
            MediaState.PreviousTitle = MediaState.LastTitle;
            MediaState.LastTitle = title;
            MediaState.LastArtist = artist;
            CoverFast.BumpGeneration();
        }

        if (titleChanged || MediaState.LastCoverHash == 0)
            await EnsureGsmtcCoverAsync(dir, title, artist, titleChanged);

        var source = MediaState.OfflineGsmtc ? "gsmtc" : "webapi";
        var durationSec = durationMs / 1000.0;
        var positionSec = positionMs / 1000.0;
        var progress = durationMs > 0 ? Math.Clamp(positionMs * 100.0 / durationMs, 0, 100) : 0;
        await NowPlayingIniWriter.WriteAsync(
            Path.Combine(dir, "nowplaying.ini"),
            title,
            artist,
            PublishedState(playbackState),
            positionSec,
            durationSec,
            progress,
            MediaState.CoverRev,
            MediaState.CoverFileName,
            source,
            seekEnabled: !MediaState.OfflineGsmtc,
            positionMs: positionMs,
            durationMs: durationMs,
            pollUtc: DateTime.UtcNow,
            statusLine: "");
        MediaState.LastWrittenTitle = title;
        MediaState.LastWrittenArtist = artist;
        MediaState.LastWrittenPositionMs = positionMs;
        MediaState.LastWrittenDurationMs = durationMs;
        MediaState.LastWrittenPlaybackState = playbackState;
        MediaState.LastWrittenPositionSec = positionSec;
        MediaState.LastWrittenDurationSec = durationSec;
        MediaState.LastWrittenProgress = progress;
        AppliedGsmtcRaw = raw;
        if (titleChanged && !MediaState.OfflineGsmtc)
            CoverFast.BeginPrefetch(dir, true);
    }

    private static void CaptureTimeline(int raw, GlobalSystemMediaTransportControlsSessionTimelineProperties timeline)
    {
        var endEarly = (long)Math.Max(0, timeline.EndTime.TotalMilliseconds);
        if (MediaState.IntendedPlaying == false && DateTime.UtcNow < MediaState.OptimisticUntilUtc)
        {
            MediaState.SpotifyGsmtcRaw = raw;
            MediaState.SpotifyGsmtcDurationMs = endEarly;
            MediaState.SpotifyGsmtcSampleUtc = DateTime.UtcNow;
            return;
        }

        var reported = (long)timeline.Position.TotalMilliseconds;
        var end = (long)Math.Max(0, timeline.EndTime.TotalMilliseconds);
        var updated = timeline.LastUpdatedTime;
        var previousRaw = MediaState.SpotifyGsmtcRaw;
        long stored = reported;
        var stamp = updated;

        if (raw == 4)
        {
            var live = reported + ClampAge(DateTimeOffset.UtcNow - updated);
            if (end > 0 && live > end)
                live = end;
            MediaState.SpotifyGsmtcPositionMs = Math.Max(0, live);
        }
        else if (previousRaw == 4)
        {
            var carried = MediaState.SpotifyTimelinePositionMs;
            if (MediaState.SpotifyTimelineUpdatedUtc != default)
                carried += ClampAge(DateTimeOffset.UtcNow - MediaState.SpotifyTimelineUpdatedUtc);
            var displayed = Math.Max(carried, MediaState.LastWrittenPositionMs);
            // why: the pause sample trails the clock; keep the higher one unless it is a real seek
            var live = reported + 2000 < displayed ? reported : Math.Max(displayed, reported);
            if (end > 0 && live > end)
                live = end;
            stored = Math.Max(0, live);
            stamp = DateTimeOffset.UtcNow;
            MediaState.SpotifyGsmtcPositionMs = stored;
        }
        else
        {
            if (Math.Abs(reported - MediaState.SpotifyTimelinePositionMs) > 2500 && MediaState.SpotifyTimelinePositionMs > 0)
                stored = reported;
            else if (MediaState.SpotifyTimelinePositionMs > 0)
                stored = MediaState.SpotifyTimelinePositionMs;
            if (end > 0 && stored > end)
                stored = end;
            stamp = DateTimeOffset.UtcNow;
            MediaState.SpotifyGsmtcPositionMs = Math.Max(0, stored);
        }

        MediaState.SpotifyGsmtcRaw = raw;
        MediaState.SpotifyGsmtcDurationMs = end;
        MediaState.SpotifyTimelinePositionMs = raw == 4 ? reported : stored;
        MediaState.SpotifyTimelineUpdatedUtc = raw == 4 ? updated : stamp;
        MediaState.SpotifyGsmtcSampleUtc = DateTime.UtcNow;
    }

    private static long ClampAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(12))
            return 0;
        return (long)age.TotalMilliseconds;
    }
}
