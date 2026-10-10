using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private bool _suppressWidgetIniWrite;
    private readonly Dictionary<string, CheckBox> _illustroChecks = new(StringComparer.OrdinalIgnoreCase);

    private void InitWidgetAndPhotos()
    {
        HubLog.Line += (msg, err) => Dispatcher.Invoke(() => AppendLog(msg, err));
        RainmeterService.EnsureCurrentPhotoSkinHook();
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
        var name = RainmeterService.ReadCurrentWidgetPhotoFileName();
        CurrentWidgetPhotoText.Text = string.IsNullOrEmpty(name)
            ? "(Widget not showing a file yet — click the widget to advance.)"
            : name;
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
        var settings = new RainmeterWidgetSettings(
            (int)WidgetSizeSlider.Value,
            (int)WidgetSecondsSlider.Value,
            (int)WidgetRadiusSlider.Value);
        WidgetSizeLabel.Text = $"{settings.Size} px";
        WidgetSecondsLabel.Text = $"{settings.Seconds} s";
        WidgetRadiusLabel.Text = $"{settings.Radius} px";
        try
        {
            RainmeterService.WriteWidgetSettings(settings);
            AppendLog($"Widget ini: Size={settings.Size}, Seconds={settings.Seconds}, Radius={settings.Radius}");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private void HideCurrentWidgetPhoto_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshCurrentWidgetPhoto();
        var name = RainmeterService.ReadCurrentWidgetPhotoFileName();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("No current widget photo detected yet.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _featured.AddExcludeFileName(name);
        RefreshExcludeList();
        AppendLog($"Excluded from widget: {name}");
    }

    private void RemoveExclude_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExcludeList.SelectedItem is not string item)
            return;
        var model = _featured.ReadModel();
        model.Excludes.RemoveAll(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
        _featured.WriteModel(model);
        RefreshExcludeList();
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
