using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PhotoHub.Services;

public sealed class ProcessRunner
{
    public event Action<string>? LineReceived;
    public event Action<string>? ErrorReceived;

    public async Task<int> RunPythonAsync(string script, IReadOnlyList<string> args, CancellationToken ct)
    {
        if (!File.Exists(PhotoPaths.PythonExe))
            throw new FileNotFoundException("Python venv not found.", PhotoPaths.PythonExe);
        if (!File.Exists(script))
            throw new FileNotFoundException("Script not found.", script);

        await WaitForMemoryAsync(ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = PhotoPaths.PythonExe,
            WorkingDirectory = Path.GetDirectoryName(script) ?? PhotoPaths.SortTools,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-u");
        psi.ArgumentList.Add(script);
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        psi.Environment["PHOTO_HUB_DATA"] = App.DataDirectory;

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();
        try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* ignore */ }

        var outTask = PumpAsync(proc.StandardOutput, LineReceived, ct);
        var errTask = PumpAsync(proc.StandardError, ErrorReceived, ct);
        await Task.WhenAll(outTask, errTask, proc.WaitForExitAsync(ct)).ConfigureAwait(false);
        return proc.ExitCode;
    }

    public async Task<int> RunPowerShellAsync(string scriptPath, CancellationToken ct)
    {
        await WaitForMemoryAsync(ct).ConfigureAwait(false);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* ignore */ }

        var outTask = PumpAsync(proc.StandardOutput, LineReceived, ct);
        var errTask = PumpAsync(proc.StandardError, s => ErrorReceived?.Invoke(s), ct);
        await Task.WhenAll(outTask, errTask, proc.WaitForExitAsync(ct)).ConfigureAwait(false);
        return proc.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string>? handler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                break;
            handler?.Invoke(line);
        }
    }

    private async Task WaitForMemoryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (GetAvailablePhysicalGb() >= 2.0)
                return;
            LineReceived?.Invoke("Waiting for memory (need about 2 GB free)…");
            await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        }
    }

    private static double GetAvailablePhysicalGb()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mem))
            return 8;
        return mem.ullAvailPhys / (1024.0 * 1024 * 1024);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
