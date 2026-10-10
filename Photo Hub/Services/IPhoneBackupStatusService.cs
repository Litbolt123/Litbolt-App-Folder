namespace PhotoHub.Services;

public sealed record BackupStatus(int PartialFiles, int ImageFiles, DateTime? LatestWrite);

public static class IPhoneBackupStatusService
{
    public static BackupStatus Scan(string backupFolder)
    {
        if (!Directory.Exists(backupFolder))
            return new BackupStatus(0, 0, null);

        var partial = 0;
        var images = 0;
        DateTime? latest = null;
        foreach (var file in Directory.EnumerateFiles(backupFolder, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            {
                partial++;
                continue;
            }
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!PhotoPaths.ImageExtensions.Contains(ext))
                continue;
            images++;
            var wt = File.GetLastWriteTimeUtc(file);
            if (latest is null || wt > latest)
                latest = wt;
        }
        return new BackupStatus(partial, images, latest);
    }

    public static bool TryLaunchSyncApp()
    {
        var exe = PhotoPaths.IPhoneSyncExe;
        if (!File.Exists(exe))
            return false;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--photo-hub-cli sync \"{PhotoPaths.IPhoneBackup}\"",
            UseShellExecute = true,
        });
        return true;
    }

    public static bool TryLaunchSyncAppGui()
    {
        var exe = PhotoPaths.IPhoneSyncExe;
        if (!File.Exists(exe))
            return false;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
        });
        return true;
    }
}
