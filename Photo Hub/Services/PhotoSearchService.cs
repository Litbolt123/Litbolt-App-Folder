namespace PhotoHub.Services;

public sealed record PhotoHit(string Path, string Person, int? Year, string FileName);

public static class PhotoSearchService
{
    public static IReadOnlyList<PhotoHit> Search(
        string? person,
        string? pairHint,
        int? year,
        string? fileNameContains,
        int maxResults = 500)
    {
        var hits = new List<PhotoHit>();
        var personFilter = person?.Trim();
        var nameFilter = fileNameContains?.Trim();

        foreach (var p in FaceGroupsService.ListPeople())
        {
            if (!string.IsNullOrEmpty(personFilter) &&
                !p.Name.Contains(personFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(pairHint) &&
                !pairHint.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var file in Directory.GetFiles(p.Path))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!PhotoPaths.ImageExtensions.Contains(ext))
                    continue;
                if (Path.GetFileName(file).StartsWith("face_", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fn = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(nameFilter) &&
                    !fn.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                int? y = null;
                if (year is not null)
                {
                    y = File.GetCreationTime(file).Year;
                    if (y != year)
                        continue;
                }
                else
                {
                    y = File.GetCreationTime(file).Year;
                }

                hits.Add(new PhotoHit(file, p.Name, y, fn));
                if (hits.Count >= maxResults)
                    return hits;
            }
        }

        return hits;
    }
}
