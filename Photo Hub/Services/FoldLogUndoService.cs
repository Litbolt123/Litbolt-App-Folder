using System.Security.Cryptography;

namespace PhotoHub.Services;

public sealed class UndoResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
}

public static class FoldLogUndoService
{
    public static UndoResult TryUndo(FoldLogEntry entry)
    {
        return entry.Kind switch
        {
            FoldLogActionKind.Copy or FoldLogActionKind.Merge => UndoCopy(entry),
            FoldLogActionKind.Rename => UndoRename(entry),
            _ => new UndoResult { Success = false, Message = "Unknown log action." },
        };
    }

    private static UndoResult UndoCopy(FoldLogEntry entry)
    {
        var src = entry.Source;
        var dst = entry.Destination;
        if (!File.Exists(dst))
            return new UndoResult { Success = false, Message = $"Destination missing: {dst}" };
        if (!File.Exists(src))
            return new UndoResult
            {
                Success = false,
                Message = $"Source missing; cannot verify hash: {src}",
            };

        if (!HashesMatch(src, dst))
            return new UndoResult
            {
                Success = false,
                Message = $"Skipped delete — destination was modified or no longer matches source:\n{dst}",
            };

        File.Delete(dst);
        FoldLogWriter.AppendHeader("Photo Hub undo COPY");
        File.AppendAllText(PhotoPaths.FoldLog,
            $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss} UNDO-COPY\t{dst}\t(deleted; matched {src})\n");
        return new UndoResult { Success = true, Message = $"Removed copy: {Path.GetFileName(dst)}" };
    }

    private static UndoResult UndoRename(FoldLogEntry entry)
    {
        var from = entry.Source;
        var to = entry.Destination;
        if (!Directory.Exists(to) && !File.Exists(to))
            return new UndoResult { Success = false, Message = $"Rename target missing: {to}" };
        if (Directory.Exists(from) || File.Exists(from))
            return new UndoResult { Success = false, Message = $"Cannot undo — original path exists: {from}" };

        if (Directory.Exists(to))
            Directory.Move(to, from);
        else
            File.Move(to, from);

        FoldLogWriter.AppendHeader("Photo Hub undo RENAME");
        File.AppendAllText(PhotoPaths.FoldLog,
            $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss} UNDO-RENAME\t{to}\t->\t{from}\n");
        return new UndoResult { Success = true, Message = $"Renamed back: {Path.GetFileName(to)} → {Path.GetFileName(from)}" };
    }

    private static bool HashesMatch(string pathA, string pathB)
    {
        var ha = Sha256File(pathA);
        var hb = Sha256File(pathB);
        return ha.SequenceEqual(hb);
    }

    private static byte[] Sha256File(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return sha.ComputeHash(stream);
    }
}
