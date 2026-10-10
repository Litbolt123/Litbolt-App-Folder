namespace PhotoHub.Services;

public sealed record PersonFolder(string Name, string Path, int PhotoCount, string? ThumbnailPath);

public static class FaceGroupsService
{
    public static bool IsFaceCropFileName(string fileName)
    {
        return fileName.StartsWith("face_", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("_face_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Full copies in folder; if only face_* reference crops exist, count those instead.</summary>
    public static int CountPhotosInPersonFolder(string personDir)
    {
        var images = Directory.GetFiles(personDir)
            .Where(f => PhotoPaths.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();
        var fullPhotos = images.Where(f => !IsFaceCropFileName(Path.GetFileName(f))).ToList();
        if (fullPhotos.Count > 0)
            return fullPhotos.Count;
        return images.Count(f => IsFaceCropFileName(Path.GetFileName(f)));
    }

    public static string? PickPersonThumbnail(string personDir)
    {
        var images = Directory.GetFiles(personDir)
            .Where(f => PhotoPaths.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();
        return images.FirstOrDefault(f => IsFaceCropFileName(Path.GetFileName(f)))
               ?? images.FirstOrDefault(f => !IsFaceCropFileName(Path.GetFileName(f)));
    }

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
            list.Add(new PersonFolder(
                name,
                dir,
                CountPhotosInPersonFolder(dir),
                PickPersonThumbnail(dir)));
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
