namespace PhotoHub.Services;

public static class HeicConvertService
{
    public static async Task<int> ConvertFolderAsync(string folder, CancellationToken ct)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);
        var count = 0;
        foreach (var src in PhotoToolsService.EnumerateImages(folder))
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext is not ".heic" and not ".heif")
                continue;
            var dest = Path.Combine(Path.GetDirectoryName(src)!,
                Path.GetFileNameWithoutExtension(src) + ".jpg");
            if (File.Exists(dest))
            {
                HubLog.Info($"Skip (JPG exists): {Path.GetFileName(dest)}");
                continue;
            }
            await Task.Run(() => WidgetRefreshService.ConvertHeicToJpegPublic(src, dest), ct);
            count++;
            HubLog.Info($"Converted {Path.GetFileName(src)} → {Path.GetFileName(dest)}");
        }
        return count;
    }
}
