using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoHub.Services;

public sealed record FaceCropBackfillResult(
    int Copied,
    int SkippedPresent,
    int Unresolved,
    IReadOnlyList<string> Messages);

/// <summary>Resolve face_* crops to People-tree originals and COPY into the person folder.</summary>
public static class FaceCropBackfillService
{
    private static readonly Regex NumberedFaceCrop = new(@"^_?face_\d+\.jpg$", RegexOptions.IgnoreCase);
    private static readonly Regex StemFaceCrop = new(@"^_?face_(.+)\.jpg$", RegexOptions.IgnoreCase);
    private static readonly Regex GroupFolder = new(@"^Group \d+ - (.+)$");

    private static Dictionary<string, int>? _sha1ToMetaIndex;
    private static List<FaceMetaEntry>? _meta;
    private static Dictionary<string, Dictionary<string, int[]>>? _invFolders;
    private static Dictionary<string, List<string>>? _peopleByBaseName;

    public static FaceCropBackfillResult BackfillPersonFolder(string personDir)
    {
        if (!Directory.Exists(personDir))
            throw new DirectoryNotFoundException(personDir);

        EnsureIndexesLoaded();
        var personName = Path.GetFileName(personDir);
        var messages = new List<string>();
        var copied = 0;
        var skipped = 0;
        var unresolved = 0;

        FoldLogWriter.AppendHeader($"Photo Hub face-crop backfill ({personName})");

        foreach (var cropPath in Directory.GetFiles(personDir).Where(IsFaceCropPath).OrderBy(Path.GetFileName))
        {
            var cropName = Path.GetFileName(cropPath);
            var source = ResolveSourcePhoto(cropPath, cropName, personName);
            if (string.IsNullOrEmpty(source))
            {
                unresolved++;
                messages.Add($"No source for {cropName}");
                continue;
            }

            if (!File.Exists(source))
            {
                unresolved++;
                messages.Add($"Missing file: {source} (from {cropName})");
                continue;
            }

            var dest = Path.Combine(personDir, Path.GetFileName(source));
            if (File.Exists(dest))
            {
                skipped++;
                continue;
            }

            File.Copy(source, dest, overwrite: false);
            FoldLogWriter.LogCopy(source, dest, "photo-hub-face-backfill");
            copied++;
        }

        return new FaceCropBackfillResult(copied, skipped, unresolved, messages);
    }

    private static bool IsFaceCropPath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg")
            return false;
        return FaceGroupsService.IsFaceCropFileName(Path.GetFileName(path));
    }

    private static string? ResolveSourcePhoto(string cropPath, string cropName, string personName)
    {
        if (TryResolveBySha1(cropPath, out var byHash))
            return byHash;

        if (TryResolveByStem(cropName, out var byStem))
            return byStem;

        if (NumberedFaceCrop.IsMatch(cropName) && TryResolveByInventory(cropPath, cropName, personName, out var byInv))
            return byInv;

        return TryResolveFromFoldLog(cropPath, personName);
    }

    private static bool TryResolveBySha1(string cropPath, out string? peoplePath)
    {
        peoplePath = null;
        var h = Sha1Hex(cropPath);
        if (_sha1ToMetaIndex!.TryGetValue(h, out var idx))
        {
            peoplePath = _meta![idx].People;
            return true;
        }

        return false;
    }

    private static bool TryResolveByStem(string cropName, out string? peoplePath)
    {
        peoplePath = null;
        var m = StemFaceCrop.Match(cropName);
        if (!m.Success)
            return false;
        var stem = m.Groups[1].Value;
        if (stem.Length > 0 && stem.All(char.IsDigit))
            return false;

        foreach (var ext in PhotoPaths.ImageExtensions)
        {
            var key = (stem + ext).ToLowerInvariant();
            if (_peopleByBaseName!.TryGetValue(key, out var hits) && hits.Count > 0)
            {
                peoplePath = hits[0];
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveByInventory(
        string cropPath,
        string cropName,
        string personName,
        out string? peoplePath)
    {
        peoplePath = null;
        var indices = new List<int>();
        foreach (var (folderKey, crops) in _invFolders!)
        {
            if (!InvFolderMatchesPerson(folderKey, personName))
                continue;
            if (crops.TryGetValue(cropName, out var ids))
                indices.AddRange(ids);
        }

        indices = indices.Distinct().ToList();
        if (indices.Count == 0)
            return false;

        if (indices.Count == 1)
        {
            peoplePath = MetaPeoplePath(indices[0]);
            return peoplePath is not null;
        }

        var h = Sha1Hex(cropPath);
        foreach (var idx in indices)
        {
            var entry = _meta![idx];
            if (!File.Exists(entry.Crop))
                continue;
            if (Sha1Hex(entry.Crop) == h)
            {
                peoplePath = entry.People;
                return true;
            }
        }

        peoplePath = MetaPeoplePath(indices[0]);
        return peoplePath is not null;
    }

    private static string? MetaPeoplePath(int index)
    {
        if (index < 0 || index >= _meta!.Count)
            return null;
        return _meta[index].People;
    }

    private static string? TryResolveFromFoldLog(string cropPath, string personName)
    {
        if (!File.Exists(PhotoPaths.FoldLog))
            return null;

        var cropFull = Path.GetFullPath(cropPath);
        foreach (var line in File.ReadLines(PhotoPaths.FoldLog))
        {
            if (!line.Contains("COPY\t", StringComparison.Ordinal))
                continue;
            var parts = line.Split('\t');
            if (parts.Length < 4)
                continue;
            var src = parts[1];
            var dst = parts[3].Trim();
            if (!dst.Contains(personName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(Path.GetFullPath(dst), cropFull, StringComparison.OrdinalIgnoreCase))
                continue;
            if (File.Exists(src)
                && !FaceGroupsService.IsFaceCropFileName(Path.GetFileName(src))
                && src.StartsWith(PhotoPaths.PeopleRoot, StringComparison.OrdinalIgnoreCase))
                return src;
        }

        return null;
    }

    private static bool InvFolderMatchesPerson(string invFolderKey, string personDirName)
    {
        if (string.Equals(invFolderKey, personDirName, StringComparison.OrdinalIgnoreCase))
            return true;
        var m = GroupFolder.Match(invFolderKey);
        if (!m.Success)
            return false;
        var label = m.Groups[1].Value.Trim();
        if (string.Equals(label, personDirName, StringComparison.OrdinalIgnoreCase))
            return true;
        return label.StartsWith(personDirName, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureIndexesLoaded()
    {
        if (_meta is not null)
            return;

        var metaPath = Path.Combine(PhotoPaths.SortTools, "faces_meta.json");
        if (!File.Exists(metaPath))
            throw new FileNotFoundException("Face database not found.", metaPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
        _meta = doc.RootElement.EnumerateArray()
            .Select((el, i) => new FaceMetaEntry(
                i,
                el.GetProperty("people").GetString() ?? "",
                el.TryGetProperty("crop", out var c) ? c.GetString() ?? "" : ""))
            .ToList();

        _sha1ToMetaIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _meta)
        {
            if (string.IsNullOrEmpty(entry.Crop) || !File.Exists(entry.Crop))
                continue;
            var h = Sha1Hex(entry.Crop);
            _sha1ToMetaIndex.TryAdd(h, entry.Index);
        }

        _invFolders = new Dictionary<string, Dictionary<string, int[]>>(StringComparer.OrdinalIgnoreCase);
        var invPath = Path.Combine(PhotoPaths.SortTools, "audit", "inv.json");
        if (File.Exists(invPath))
        {
            using var inv = JsonDocument.Parse(File.ReadAllText(invPath));
            if (inv.RootElement.TryGetProperty("folders", out var folders))
            {
                foreach (var folder in folders.EnumerateObject())
                {
                    var cropMap = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var crop in folder.Value.EnumerateObject())
                    {
                        var ids = crop.Value.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                        cropMap[crop.Name] = ids;
                    }

                    _invFolders[folder.Name] = cropMap;
                }
            }
        }

        _peopleByBaseName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(PhotoPaths.PeopleRoot))
        {
            foreach (var file in Directory.EnumerateFiles(PhotoPaths.PeopleRoot, "*.*", SearchOption.AllDirectories))
            {
                var dir = Path.GetFileName(Path.GetDirectoryName(file) ?? "");
                if (dir.StartsWith("_", StringComparison.Ordinal))
                    continue;
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!PhotoPaths.ImageExtensions.Contains(ext))
                    continue;
                var key = Path.GetFileName(file).ToLowerInvariant();
                if (!_peopleByBaseName.TryGetValue(key, out var list))
                {
                    list = [];
                    _peopleByBaseName[key] = list;
                }

                list.Add(file);
            }
        }
    }

    private static string Sha1Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record FaceMetaEntry(int Index, string People, string Crop);
}
