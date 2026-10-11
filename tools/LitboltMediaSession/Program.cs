using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using LitboltMediaSession;
using Windows.Media.Control;
using Windows.Storage.Streams;

const string MutexName = @"Global\LitboltMediaSessionWatch";
const int WatchIntervalMs = 350;

static string MediaDir()
{
    var env = Environment.GetEnvironmentVariable("LITBOLT_MEDIA_DIR");
    if (!string.IsNullOrWhiteSpace(env))
        return env.TrimEnd('\\', '/');

    return AppContext.BaseDirectory.TrimEnd('\\', '/');
}

static string StopFlagPath(string dir) => Path.Combine(dir, "watch.stop");
static string CommandPath(string dir) => Path.Combine(dir, "command.txt");

static async Task<GlobalSystemMediaTransportControlsSession?> GetSessionAsync()
{
    var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    return mgr.GetCurrentSession();
}

static int PlaybackState(GlobalSystemMediaTransportControlsSessionPlaybackInfo info) =>
    info.PlaybackStatus switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => 1,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => 2,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => 0,
        _ => 0,
    };

static string EscapeIni(string value) =>
    value.Replace("\r", " ").Replace("\n", " ").Trim();

static string FormatTime(double totalSeconds)
{
    if (totalSeconds < 0 || double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds))
        return "0:00";
    var ts = TimeSpan.FromSeconds(totalSeconds);
    return ts.Hours > 0
        ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
        : $"{ts.Minutes}:{ts.Seconds:D2}";
}

static async Task WriteStateAsync(string dir)
{
    Directory.CreateDirectory(dir);
    var statePath = Path.Combine(dir, "nowplaying.ini");
    var coverPath = Path.Combine(dir, "cover.jpg");

    var session = await GetSessionAsync();
    if (session is null)
    {
        MediaState.LastTitle = null;
        await WriteIniAsync(statePath, "", "", 0, 0, 0, 0, MediaState.CoverRev);
        return;
    }

    var props = await session.TryGetMediaPropertiesAsync();
    var playback = session.GetPlaybackInfo();
    var timeline = session.GetTimelineProperties();
    var duration = timeline.EndTime.TotalSeconds;
    var position = timeline.Position.TotalSeconds;
    var progress = duration > 0 ? Math.Clamp(position / duration * 100.0, 0, 100) : 0;
    var title = props.Title ?? "";
    var artist = props.Artist ?? "";

    if (!string.Equals(MediaState.LastTitle, title, StringComparison.Ordinal))
    {
        MediaState.LastTitle = title;
        MediaState.CoverRev++;
    }

    if (props.Thumbnail is not null)
    {
        try
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            var tmpCover = coverPath + ".tmp";
            await File.WriteAllBytesAsync(tmpCover, bytes);
            File.Move(tmpCover, coverPath, true);
            MediaState.CoverRev++;
        }
        catch
        {
            // keep previous cover.jpg
        }
    }

    await WriteIniAsync(
        statePath,
        title,
        artist,
        PlaybackState(playback),
        position,
        duration,
        progress,
        MediaState.CoverRev);
}

static async Task WriteIniAsync(
    string statePath,
    string title,
    string artist,
    int state,
    double position,
    double duration,
    double progress,
    long coverRevision)
{
    var sb = new StringBuilder();
    sb.AppendLine("[Media]");
    sb.AppendLine($"Title={EscapeIni(title)}");
    sb.AppendLine($"Artist={EscapeIni(artist)}");
    sb.AppendLine($"State={state}");
    sb.AppendLine($"Position={position.ToString("0.###", CultureInfo.InvariantCulture)}");
    sb.AppendLine($"Duration={duration.ToString("0.###", CultureInfo.InvariantCulture)}");
    sb.AppendLine($"Progress={progress.ToString("0.##", CultureInfo.InvariantCulture)}");
    sb.AppendLine($"PositionText={FormatTime(position)}");
    sb.AppendLine($"DurationText={FormatTime(duration)}");
    sb.AppendLine($"CoverRev={coverRevision}");
    var tmp = statePath + ".tmp";
    await File.WriteAllTextAsync(tmp, sb.ToString(), Encoding.UTF8);
    File.Move(tmp, statePath, true);
}

static async Task<int> RunTransportAsync(string verb)
{
    var session = await GetSessionAsync();
    if (session is null)
        return 1;

    switch (verb.ToLowerInvariant())
    {
        case "playpause":
            await session.TryTogglePlayPauseAsync();
            break;
        case "next":
            await session.TrySkipNextAsync();
            break;
        case "previous":
        case "prev":
            await session.TrySkipPreviousAsync();
            break;
        default:
            return 2;
    }

    return 0;
}

static async Task ProcessCommandFileAsync(string dir)
{
    var path = CommandPath(dir);
    if (!File.Exists(path))
        return;

    string? line = null;
    try
    {
        line = (await File.ReadAllTextAsync(path)).Trim();
        File.Delete(path);
    }
    catch
    {
        return;
    }

    if (string.IsNullOrEmpty(line))
        return;

    await RunTransportAsync(line);
    await WriteStateAsync(dir);
}

static async Task WatchLoopAsync(string dir, CancellationToken ct)
{
    File.Delete(StopFlagPath(dir));
    MediaState.LastTitle = null;
    MediaState.CoverRev = 0;

    while (!ct.IsCancellationRequested)
    {
        if (File.Exists(StopFlagPath(dir)))
        {
            File.Delete(StopFlagPath(dir));
            break;
        }

        if (!IsAnyRainmeterRunning())
            break;

        await ProcessCommandFileAsync(dir);
        await WriteStateAsync(dir);
        try
        {
            await Task.Delay(WatchIntervalMs, ct);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

static bool TryAcquireWatchMutex()
{
    try
    {
        MediaState.WatchMutex = new Mutex(true, MutexName, out var created);
        if (!created && !MediaState.WatchMutex.WaitOne(0))
            return false;
        return true;
    }
    catch
    {
        return false;
    }
}

static string WatchPidPath(string dir) => Path.Combine(dir, "watch.pid");

static void DeleteStalePidFile(string dir)
{
    try
    {
        File.Delete(WatchPidPath(dir));
    }
    catch
    {
        // ignore
    }
}

static bool IsWatchProcessAlive(string dir)
{
    var pidPath = WatchPidPath(dir);
    if (!File.Exists(pidPath))
        return false;

    if (!int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid))
    {
        DeleteStalePidFile(dir);
        return false;
    }

    try
    {
        using var proc = Process.GetProcessById(pid);
        if (proc.HasExited)
        {
            DeleteStalePidFile(dir);
            return false;
        }

        return proc.ProcessName.Equals("LitboltMediaSession", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        DeleteStalePidFile(dir);
        return false;
    }
}

static bool IsAnyRainmeterRunning() =>
    Process.GetProcessesByName("Rainmeter").Length > 0;

static async Task<int> RunWatchAsync(string dir)
{
    if (!TryAcquireWatchMutex())
        return 0;

    await File.WriteAllTextAsync(WatchPidPath(dir), Environment.ProcessId.ToString());

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    try
    {
        await WatchLoopAsync(dir, cts.Token);
    }
    finally
    {
        try { File.Delete(WatchPidPath(dir)); } catch { /* ignore */ }
        MediaState.WatchMutex?.ReleaseMutex();
    }

    return 0;
}

var dir = MediaDir();
var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "poll";

try
{
    switch (cmd)
    {
        case "watch":
        case "ensure-watch":
            if (IsWatchProcessAlive(dir))
                return 0;

            var exe = Environment.ProcessPath ?? Path.Combine(dir, "LitboltMediaSession.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "watch-loop",
                WorkingDirectory = dir,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return 0;
        case "watch-loop":
            return await RunWatchAsync(dir);
        case "stop":
            File.WriteAllText(StopFlagPath(dir), "1");
            try
            {
                var pidPath = WatchPidPath(dir);
                if (File.Exists(pidPath) && int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid))
                    Process.GetProcessById(pid).Kill(true);
            }
            catch
            {
                // process may already have exited
            }

            return 0;
        case "poll":
        case "sync":
            await WriteStateAsync(dir);
            return 0;
        case "playpause":
        case "next":
        case "previous":
        case "prev":
            await RunTransportAsync(cmd);
            await WriteStateAsync(dir);
            return 0;
        default:
            Console.Error.WriteLine("Usage: LitboltMediaSession [watch|stop|poll|playpause|next|previous]");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
