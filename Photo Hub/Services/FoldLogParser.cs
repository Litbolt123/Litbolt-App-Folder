namespace PhotoHub.Services;

public enum FoldLogActionKind { Copy, Rename, Merge, Unknown }

public sealed record FoldLogEntry(
    int LineNumber,
    FoldLogActionKind Kind,
    string Source,
    string Destination,
    string RawLine);

public static class FoldLogParser
{
    public static IReadOnlyList<FoldLogEntry> ParseRecent(int maxEntries = 200)
    {
        if (!File.Exists(PhotoPaths.FoldLog))
            return [];
        var lines = File.ReadAllLines(PhotoPaths.FoldLog);
        var entries = new List<FoldLogEntry>();
        for (var i = 0; i < lines.Length; i++)
        {
            var entry = TryParse(lines[i], i + 1);
            if (entry is not null)
                entries.Add(entry);
        }
        return entries.TakeLast(maxEntries).Reverse().ToList();
    }

    private static FoldLogEntry? TryParse(string line, int lineNumber)
    {
        var t = line.Trim();
        if (t.Length == 0 || t.StartsWith("==="))
            return null;

        string? src = null;
        string? dst = null;
        FoldLogActionKind kind;

        if (t.Contains(" COPY\t", StringComparison.Ordinal))
        {
            kind = FoldLogActionKind.Copy;
            var idx = t.IndexOf(" COPY\t", StringComparison.Ordinal);
            var rest = t[(idx + " COPY\t".Length)..];
            var parts = rest.Split('\t');
            if (parts.Length >= 3 && parts[1] == "->")
            {
                src = parts[0].Trim();
                dst = parts[2].Trim();
            }
        }
        else if (t.StartsWith("RENAME\t", StringComparison.Ordinal))
        {
            kind = FoldLogActionKind.Rename;
            var parts = t.Split('\t');
            if (parts.Length >= 4 && parts[2] == "->")
            {
                src = parts[1].Trim();
                dst = parts[3].Trim();
            }
        }
        else if (t.StartsWith("MERGE\t", StringComparison.Ordinal))
        {
            kind = FoldLogActionKind.Merge;
            var parts = t.Split('\t');
            if (parts.Length >= 4 && parts[2] == "->")
            {
                src = parts[1].Trim();
                dst = parts[3].Trim();
            }
        }
        else
            return null;

        if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst))
            return null;
        return new FoldLogEntry(lineNumber, kind, src, dst, line);
    }
}
