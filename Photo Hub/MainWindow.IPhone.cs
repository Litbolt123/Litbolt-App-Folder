using System.Diagnostics;
using System.Windows;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private void RefreshBackupStatus_OnClick(object sender, RoutedEventArgs e) => UpdateBackupStatus();

    private void UpdateBackupStatus()
    {
        var folder = IPhoneBackupBox.Text.Trim();
        var st = IPhoneBackupStatusService.Scan(folder);
        BackupStatusText.Text = Directory.Exists(folder)
            ? $"{st.ImageFiles} images in backup. {st.PartialFiles} incomplete (.partial) files."
            : "Backup folder not found.";
        if (st.PartialFiles > 0)
            BackupStatusText.Text += " Remove or re-sync stuck partials before sorting.";
        if (st.LatestWrite is not null)
            BackupStatusText.Text += $" Latest file: {st.LatestWrite.Value.ToLocalTime():g}.";
    }

    private void LaunchSyncGui_OnClick(object sender, RoutedEventArgs e)
    {
        if (!IPhoneBackupStatusService.TryLaunchSyncAppGui())
        {
            MessageBox.Show($"Build or install iPhone Photo Sync at:\n{PhotoPaths.IPhoneSyncExe}",
                "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AppendLog("Opened iPhone Photo Sync.");
    }

    private async void RunHeadlessSync_OnClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(PhotoPaths.IPhoneSyncExe))
        {
            AppendLog($"Sync app missing: {PhotoPaths.IPhoneSyncExe}", true);
            return;
        }
        AppendLog("Headless sync started (unlock phone, Trust this PC)…");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = PhotoPaths.IPhoneSyncExe,
                Arguments = $"--photo-hub-cli sync \"{IPhoneBackupBox.Text.Trim()}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { /* ignore */ }
            var outText = await p.StandardOutput.ReadToEndAsync();
            var errText = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            foreach (var line in outText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                AppendLog(line.Trim());
            if (!string.IsNullOrWhiteSpace(errText))
                AppendLog(errText.Trim(), true);
            AppendLog(p.ExitCode == 0 ? "Sync finished." : $"Sync exit {p.ExitCode}.", p.ExitCode != 0);
            UpdateBackupStatus();
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private void GoSortAfterSync_OnClick(object sender, RoutedEventArgs e)
    {
        SourceFolderBox.Text = IPhoneBackupBox.Text.Trim();
        MainTabs.SelectedIndex = 0;
    }
}
