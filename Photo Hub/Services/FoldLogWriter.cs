namespace PhotoHub.Services;

public static class FoldLogWriter
{
    public static void AppendHeader(string title)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PhotoPaths.FoldLog)!);
        File.AppendAllText(PhotoPaths.FoldLog,
            $"\n=== {title} {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
    }

    public static void LogCopy(string src, string dst, string tag = "photo-hub")
    {
        File.AppendAllText(PhotoPaths.FoldLog,
            $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss} COPY\t{src}\t->\t{dst}\t({tag})\n");
    }

    public static void LogRename(string from, string to)
    {
        File.AppendAllText(PhotoPaths.FoldLog, $"RENAME\t{from}\t->\t{to}\n");
    }

    public static void LogMerge(string fromFolder, string toFolder, string file)
    {
        File.AppendAllText(PhotoPaths.FoldLog,
            $"MERGE\t{Path.Combine(fromFolder, file)}\t->\t{Path.Combine(toFolder, file)}\n");
    }
}
