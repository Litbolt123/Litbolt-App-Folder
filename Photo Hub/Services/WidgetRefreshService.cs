namespace PhotoHub.Services;

public static class WidgetRefreshService
{
    private static readonly string[] WidgetSourceExts = [".jpg", ".jpeg", ".png", ".heic", ".heif"];

    public static int RefreshFromBackup(string backupFolder, string widgetFolder, int count, CancellationToken ct)
    {
        count = Math.Clamp(count, 1, 500);
        if (PathsEqual(backupFolder, widgetFolder))
            throw new InvalidOperationException("Widget folder must differ from backup folder.");

        if (!Directory.Exists(backupFolder))
            throw new DirectoryNotFoundException($"Backup folder not found: {backupFolder}");

        var candidates = Directory.EnumerateFiles(backupFolder, "*", SearchOption.AllDirectories)
            .Where(p => !PhotoToolsService.IsImportantPath(p))
            .Where(p => WidgetSourceExts.Contains(Path.GetExtension(p).ToLowerInvariant()))
            .Where(p => !Path.GetFileName(p).EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        Directory.CreateDirectory(widgetFolder);
        ClearWidgetSlots(widgetFolder);

        var written = 0;
        foreach (var source in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (written >= count)
                break;
            var index = written + 1;
            var dest = Path.Combine(widgetFolder, $"widget-{index:00}.jpg");
            if (IsHeic(source))
            {
                ConvertHeicToJpeg(source, dest);
            }
            else
            {
                File.Copy(source, dest, overwrite: true);
            }
            written++;
        }
        HubLog.Info($"Widget refresh from backup: {written} photos into {widgetFolder}.");
        return written;
    }

    public static int RefreshFromPeople(string widgetFolder, int perPerson, FeaturedPeopleService featured, CancellationToken ct)
    {
        perPerson = Math.Clamp(perPerson, 1, 50);
        Directory.CreateDirectory(widgetFolder);
        var people = featured.EnabledPeople().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var taken = new List<string>();
        foreach (var person in people)
        {
            ct.ThrowIfCancellationRequested();
            var dir = Path.Combine(PhotoPaths.FaceGroups, person);
            if (!Directory.Exists(dir))
                continue;
            var photos = Directory.GetFiles(dir)
                .Where(f => PhotoPaths.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Path.GetFileName(f).StartsWith("face_", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(perPerson);
            foreach (var src in photos)
                taken.Add(src);
        }

        ClearWidgetSlots(widgetFolder);
        var written = 0;
        foreach (var source in taken)
        {
            ct.ThrowIfCancellationRequested();
            written++;
            var ext = Path.GetExtension(source).ToLowerInvariant();
            var dest = Path.Combine(widgetFolder, $"people-{written:00}.jpg");
            if (ext is ".heic" or ".heif")
                ConvertHeicToJpeg(source, dest);
            else if (ext is ".jpg" or ".jpeg")
                File.Copy(source, dest, overwrite: true);
            else
            {
                var tempDest = dest;
                RunPythonConvert(source, tempDest);
            }
        }
        HubLog.Info($"Widget refresh from People: {written} copies for {people.Count} featured people.");
        return written;
    }

    private static void ClearWidgetSlots(string widgetFolder)
    {
        foreach (var f in Directory.GetFiles(widgetFolder))
        {
            var name = Path.GetFileName(f);
            if (name.StartsWith("widget-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("people-", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(f);
            }
        }
    }

    private static bool IsHeic(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".heic" or ".heif";

    public static void ConvertHeicToJpegPublic(string source, string dest) => ConvertHeicToJpeg(source, dest);

    private static void ConvertHeicToJpeg(string source, string dest)
    {
        if (!File.Exists(PhotoPaths.PythonExe))
            throw new FileNotFoundException("Python venv missing for HEIC conversion.");
        var script = Path.Combine(PhotoPaths.SortTools, "photo_hub", "convert_one.py");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = PhotoPaths.PythonExe,
            Arguments = $"\"{script}\" \"{source}\" \"{dest}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        try { p.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal; } catch { /* ignore */ }
        p.WaitForExit();
        if (p.ExitCode != 0 || !File.Exists(dest))
            throw new InvalidOperationException($"HEIC convert failed: {source}");
    }

    private static void RunPythonConvert(string source, string dest) => ConvertHeicToJpeg(source, dest);

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}
