using System.Globalization;

namespace LitboltMediaSession;

internal static class MediaStateStore
{
    private const string FileName = "media-state.ini";

    public static void Load(string dir)
    {
        var path = Path.Combine(dir, FileName);
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim();
            switch (key)
            {
                case "LastTitle":
                    MediaState.LastTitle = val;
                    break;
                case "CoverRev":
                    if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rev))
                        MediaState.CoverRev = rev;
                    break;
                case "LastCoverHash":
                    if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hash))
                        MediaState.LastCoverHash = hash;
                    break;
                case "CoverFileName":
                    if (!string.IsNullOrEmpty(val))
                        MediaState.CoverFileName = val;
                    break;
            }
        }
    }

    public static void Save(string dir)
    {
        var path = Path.Combine(dir, FileName);
        var tmp = path + ".tmp";
        var lines = new[]
        {
            "[State]",
            $"LastTitle={MediaState.LastTitle ?? ""}",
            $"CoverRev={MediaState.CoverRev.ToString(CultureInfo.InvariantCulture)}",
            $"LastCoverHash={MediaState.LastCoverHash.ToString(CultureInfo.InvariantCulture)}",
            $"CoverFileName={MediaState.CoverFileName}",
        };
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, path, true);
    }
}
