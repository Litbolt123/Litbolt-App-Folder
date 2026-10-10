namespace PhotoHub.Services;

public sealed record PersonFolder(string Name, string Path, int PhotoCount, string? ThumbnailPath);

public static class FaceGroupsService
{
    public static IReadOnlyList<PersonFolder> ListPeople()
    {
        if (!Directory.Exists(PhotoPaths.FaceGroups))
            return [];

        var list = new List<PersonFolder>();
        foreach (var dir in Directory.GetDirectories(PhotoPaths.FaceGroups).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith('_'))
                continue;
            var files = Directory.GetFiles(dir)
                .Where(f => PhotoPaths.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Path.GetFileName(f).StartsWith("face_", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var thumb = Directory.GetFiles(dir)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("face_", StringComparison.OrdinalIgnoreCase));
            list.Add(new PersonFolder(name, dir, files.Count, thumb));
        }
        return list;
    }

    public static void RenamePerson(string oldPath, string newName)
    {
        var safe = newName.Trim();
        if (string.IsNullOrWhiteSpace(safe))
            throw new ArgumentException("Name is required.");
        var dest = Path.Combine(PhotoPaths.FaceGroups, safe);
        if (Directory.Exists(dest))
            throw new InvalidOperationException($"A folder named \"{safe}\" already exists.");
        Directory.Move(oldPath, dest);
        FoldLogWriter.AppendHeader("Photo Hub rename");
        FoldLogWriter.LogRename(oldPath, dest);
    }

    public static void MergePeople(string fromPath, string intoPath)
    {
        FoldLogWriter.AppendHeader("Photo Hub merge");
        foreach (var file in Directory.GetFiles(fromPath))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(intoPath, name);
            if (File.Exists(dest))
            {
                var stem = Path.GetFileNameWithoutExtension(name);
                var ext = Path.GetExtension(name);
                var i = 2;
                while (File.Exists(dest))
                {
                    dest = Path.Combine(intoPath, $"{stem}_{i}{ext}");
                    i++;
                }
            }
            File.Copy(file, dest);
            FoldLogWriter.LogMerge(fromPath, intoPath, Path.GetFileName(dest));
        }
    }
}
