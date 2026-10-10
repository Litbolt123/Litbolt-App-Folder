using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private bool _suppressWidgetIniWrite;
    private readonly Dictionary<string, CheckBox> _illustroChecks = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _widgetIniDebounce;
    private bool _widgetIniWritePending;

    private void InitWidgetAndPhotos()
    {
        HubLog.Line += (msg, err) => Dispatcher.Invoke(() => AppendLog(msg, err));
        RainmeterService.EnsureCurrentPhotoSkinHook();
        _widgetIniDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _widgetIniDebounce.Tick += (_, _) =>
        {
            _widgetIniDebounce!.Stop();
            CommitWidgetIniWrite();
        };
        _suppressWidgetIniWrite = true;
        var s = RainmeterService.ReadWidgetSettings();
        WidgetSizeSlider.Value = s.Size;
        WidgetSecondsSlider.Value = s.Seconds;
        WidgetRadiusSlider.Value = s.Radius;
        WidgetSizeLabel.Text = $"{s.Size} px";
        WidgetSecondsLabel.Text = $"{s.Seconds} s";
        WidgetRadiusLabel.Text = $"{s.Radius} px";
        PhotoWidgetOnCheck.IsChecked = RainmeterPreferencesStore.Load().PhotoWidgetActive;
        _suppressWidgetIniWrite = false;
        RefreshCurrentWidgetPhoto();
        BuildIllustroChecks();
        RefreshExcludeList();
    }

    private void RefreshCurrentWidgetPhoto()
    {
        var path = RainmeterService.ReadCurrentWidgetPhotoPath();
        var name = string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
        CurrentWidgetPhotoText.Text = string.IsNullOrEmpty(name)
            ? "(Widget not showing a file yet — click the widget to advance.)"
            : $"{name}\n{path}";
    }

    private void BuildIllustroChecks()
    {
        IllustroChecksPanel.Children.Clear();
        _illustroChecks.Clear();
        var prefs = RainmeterPreferencesStore.Load();
        foreach (var skin in RainmeterService.ListIllustroSkins())
        {
            var on = prefs.IllustroSkins.GetValueOrDefault(skin.ConfigKey, false);
            var cb = new CheckBox
            {
                Content = skin.DisplayName,
                IsChecked = on,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 4, 16, 4),
            };
            cb.Checked += (_, _) => SetIllustro(skin.ConfigKey, true);
            cb.Unchecked += (_, _) => SetIllustro(skin.ConfigKey, false);
            _illustroChecks[skin.ConfigKey] = cb;
            IllustroChecksPanel.Children.Add(cb);
        }
    }

    private void SetIllustro(string key, bool on)
    {
        RainmeterService.SetIllustroActive(key, on);
        var prefs = RainmeterPreferencesStore.Load();
        prefs.IllustroSkins[key] = on;
        RainmeterPreferencesStore.Save(prefs);
    }

    private void RefreshExcludeList()
    {
        ExcludeList.Items.Clear();
        foreach (var ex in _featured.ReadModel().Excludes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            ExcludeList.Items.Add(ex);
        PinnedList.Items.Clear();
        foreach (var p in _featured.ReadModel().PinnedPhotos)
            PinnedList.Items.Add(p);
    }

    private void PhotoWidgetOnCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        var on = PhotoWidgetOnCheck.IsChecked == true;
        RainmeterService.SetPhotoWidgetActive(on);
        var prefs = RainmeterPreferencesStore.Load();
        prefs.PhotoWidgetActive = on;
        RainmeterPreferencesStore.Save(prefs);
        AppendLog(on ? "Photo Widget activated." : "Photo Widget hidden.");
    }

    private void WidgetIni_OnChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _suppressWidgetIniWrite)
            return;
        var size = (int)Math.Clamp(WidgetSizeSlider.Value, 150, 600);
        WidgetSizeLabel.Text = $"{size} px";
        WidgetSecondsLabel.Text = $"{(int)WidgetSecondsSlider.Value} s";
        WidgetRadiusLabel.Text = $"{(int)WidgetRadiusSlider.Value} px";
        _widgetIniWritePending = true;
        _widgetIniDebounce?.Stop();
        _widgetIniDebounce?.Start();
    }

    private void WidgetIni_OnCommit(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppressWidgetIniWrite)
            return;
        _widgetIniDebounce?.Stop();
        if (_widgetIniWritePending)
            CommitWidgetIniWrite();
    }

    private void CommitWidgetIniWrite()
    {
        if (!IsLoaded || _suppressWidgetIniWrite)
            return;
        _widgetIniWritePending = false;
        var settings = new RainmeterWidgetSettings(
            (int)Math.Clamp(WidgetSizeSlider.Value, 150, 600),
            (int)WidgetSecondsSlider.Value,
            (int)WidgetRadiusSlider.Value);
        WidgetSizeLabel.Text = $"{settings.Size} px";
        WidgetSecondsLabel.Text = $"{settings.Seconds} s";
        WidgetRadiusLabel.Text = $"{settings.Radius} px";
        try
        {
            RainmeterService.WriteWidgetSettings(settings);
            AppendLog($"Widget ini saved: Size={settings.Size}, Seconds={settings.Seconds}, Radius={settings.Radius}");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private async void HideCurrentWidgetPhoto_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshCurrentWidgetPhoto();
        var fullPath = RainmeterService.ReadCurrentWidgetPhotoPath();
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            MessageBox.Show(
                "No current photo path in @Resources\\current-photo.inc yet. Click the widget to change photos, then try again.",
                "Photo Hub",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var fileName = Path.GetFileName(fullPath);
        _featured.AddExcludeFileName(fileName);

        var featuredRoot = Path.GetFullPath(PhotoPaths.WidgetFeatured).TrimEnd('\\');
        var normalized = Path.GetFullPath(fullPath).TrimEnd('\\');
        if (normalized.StartsWith(featuredRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
        {
            File.Delete(fullPath);
            AppendLog($"Removed from Featured folder: {fileName}");
        }

        AppendLog($"Excluded from widget: {fileName}");
        RefreshExcludeList();
        await RunUpdateFeaturedScriptAsync();
        RainmeterService.RefreshPhotoWidget();
        RefreshCurrentWidgetPhoto();
    }

    private async void RemoveExclude_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExcludeList.SelectedItem is not string item)
        {
            MessageBox.Show("Select an excluded file name in the list first.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _featured.RemoveExcludeFileName(item);
        RefreshExcludeList();
        AppendLog($"Removed exclude: {item}");
        await RunUpdateFeaturedScriptAsync();
    }

    private async Task RunUpdateFeaturedScriptAsync()
    {
        if (!File.Exists(PhotoPaths.UpdateFeaturedScript))
        {
            AppendLog("Update-FeaturedPhotos.ps1 not found.", true);
            return;
        }
        AppendLog("Running Update-FeaturedPhotos.ps1…");
        try
        {
            var code = await _runner.RunPowerShellAsync(PhotoPaths.UpdateFeaturedScript, CancellationToken.None);
            AppendLog(code == 0 ? "Widget update finished." : $"Widget script exit code {code}.", code != 0);
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private async void RefreshWidgetFromBackup_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var count = int.TryParse(WidgetBackupCountBox.Text, out var c) ? c : 24;
            var n = await Task.Run(() =>
                WidgetRefreshService.RefreshFromBackup(
                    PhotoPaths.IPhoneBackup,
                    PhotoPaths.WidgetRoot,
                    count,
                    CancellationToken.None));
            AppendLog($"Backup → Widget Photos: {n} files.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private async void RefreshWidgetFromPeople_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var per = int.TryParse(WidgetPeoplePerBox.Text, out var p) ? p : 3;
            var n = await Task.Run(() =>
                WidgetRefreshService.RefreshFromPeople(
                    PhotoPaths.WidgetRoot,
                    per,
                    _featured,
                    CancellationToken.None));
            AppendLog($"People → Widget Photos: {n} files.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private async void StraightenFeatured_OnClick(object sender, RoutedEventArgs e)
    {
        var script = PhotoPaths.StraightenFeaturedPath;
        if (!File.Exists(script))
        {
            AppendLog($"Script missing: {script}", true);
            return;
        }
        AppendLog("Straightening Featured JPGs…");
        var code = await _runner.RunPowerShellAsync(script, CancellationToken.None);
        AppendLog(code == 0 ? "Straighten finished." : $"Straighten exit {code}.", code != 0);
    }

    private async void ConvertHeicFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder with HEIC files (copies to JPG beside each)" };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            var n = await HeicConvertService.ConvertFolderAsync(dlg.FolderName, CancellationToken.None);
            AppendLog($"HEIC convert: {n} new JPG files.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }
}
