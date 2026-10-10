namespace PhotoHub.Services;

public static class PhotoToolsService
{
    public static bool IsImportantPath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(p => string.Equals(p, "Important", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> EnumerateImages(string root)
    {
        if (!Directory.Exists(root))
            yield break;
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(dir).Equals("Important", StringComparison.OrdinalIgnoreCase))
                continue;
            if (Path.GetFileName(dir).StartsWith('_'))
                continue;
        }
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (IsImportantPath(file))
                continue;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (PhotoPaths.ImageExtensions.Contains(ext))
                yield return file;
        }
    }
}
