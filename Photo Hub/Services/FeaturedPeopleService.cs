using System.Text.RegularExpressions;

namespace PhotoHub.Services;

public sealed class FeaturedListModel
{
    public List<string> HeaderComments { get; set; } = [];
    public List<string> People { get; set; } = [];
    public List<string> Pairs { get; set; } = [];
    public List<string> PinnedPhotos { get; set; } = [];
    public List<string> Excludes { get; set; } = [];
}

public sealed class FeaturedPeopleService
{
    private static readonly Regex PairRe = new(@"^pair:\s*(.+?)\s*\+\s*(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex PhotoRe = new(@"^photo:\s*(.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex ExcludeRe = new(@"^exclude:\s*(.+)$", RegexOptions.IgnoreCase);

    public FeaturedListModel ReadModel()
    {
        var model = new FeaturedListModel();
        var inHeader = true;
        foreach (var line in ReadLines())
        {
            var t = line.Trim();
            if (inHeader && (t.StartsWith('#') || string.IsNullOrWhiteSpace(t)))
            {
                model.HeaderComments.Add(line);
                continue;
            }
            inHeader = false;
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            if (PairRe.IsMatch(t))
            {
                model.Pairs.Add(t);
                continue;
            }
            if (PhotoRe.IsMatch(t))
            {
                model.PinnedPhotos.Add(PhotoRe.Match(t).Groups[1].Value.Trim());
                continue;
            }
            if (ExcludeRe.IsMatch(t))
            {
                model.Excludes.Add(ExcludeRe.Match(t).Groups[1].Value.Trim());
                continue;
            }
            if (Directory.Exists(Path.Combine(PhotoPaths.FaceGroups, t)))
                model.People.Add(t);
        }
        if (model.HeaderComments.Count == 0)
            model.HeaderComments.Add("# Featured people — edited in Photo Hub");
        return model;
    }

    public void WriteModel(FeaturedListModel model)
    {
        var lines = new List<string>();
        lines.AddRange(model.HeaderComments);
        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            lines.Add("");
        foreach (var p in model.People.OrderBy(x => x, StringComparer.Ordinal))
            lines.Add(p);
        foreach (var pair in model.Pairs)
            lines.Add(pair.StartsWith("pair:", StringComparison.OrdinalIgnoreCase) ? pair : $"pair: {pair}");
        foreach (var photo in model.PinnedPhotos)
            lines.Add($"photo: {photo}");
        if (model.Excludes.Count > 0)
        {
            lines.Add("");
            lines.Add("# Photos you never want in the widget (file name only):");
            foreach (var ex in model.Excludes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                lines.Add($"exclude: {ex}");
        }
        WriteLines(lines);
    }

    public IReadOnlyList<string> ReadLines() =>
        File.Exists(PhotoPaths.FeaturedList)
            ? File.ReadAllLines(PhotoPaths.FeaturedList)
            : [];

    public void WriteLines(IEnumerable<string> lines) =>
        File.WriteAllLines(PhotoPaths.FeaturedList, lines);

    public HashSet<string> EnabledPeople() => new(ReadModel().People, StringComparer.Ordinal);

    public HashSet<string> EnabledPairs()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in ReadModel().Pairs)
        {
            var m = PairRe.Match(line.Trim());
            if (m.Success)
                set.Add($"{m.Groups[1].Value.Trim()} + {m.Groups[2].Value.Trim()}");
        }
        return set;
    }

    public void AddExcludeFileName(string fileName)
    {
        var model = ReadModel();
        var name = Path.GetFileName(fileName);
        if (!model.Excludes.Contains(name, StringComparer.OrdinalIgnoreCase))
            model.Excludes.Add(name);
        WriteModel(model);
    }

    public void AddPhotoLine(string fullPath)
    {
        var model = ReadModel();
        if (!model.PinnedPhotos.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            model.PinnedPhotos.Add(fullPath);
        WriteModel(model);
    }
}
