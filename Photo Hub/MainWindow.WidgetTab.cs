using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private HashSet<string> _widgetBaselinePeople = new(StringComparer.Ordinal);
    private HashSet<string> _widgetBaselinePairs = new(StringComparer.Ordinal);
    private RainmeterWidgetSettings _widgetBaselineIni = new(320, 60, 28);
    private List<PersonFolder> _widgetAllPeople = [];

    private void CaptureWidgetBaseline()
    {
        var model = _featured.ReadModel();
        _widgetBaselinePeople = new HashSet<string>(model.People, StringComparer.Ordinal);
        _widgetBaselinePairs = new HashSet<string>(
            model.Pairs.Select(NormalizePairKey).Where(k => k.Length > 0),
            StringComparer.Ordinal);
        _widgetBaselineIni = ReadWidgetSettingsFromSliders();
        UpdateWidgetPendingUi();
    }

    private static string NormalizePairKey(string line)
    {
        var t = line.Trim();
        if (t.StartsWith("pair:", StringComparison.OrdinalIgnoreCase))
            t = t["pair:".Length..].Trim();
        return t;
    }

    private RainmeterWidgetSettings ReadWidgetSettingsFromSliders() =>
        new(
            (int)Math.Clamp(WidgetSizeSlider.Value, 150, 600),
            (int)WidgetSecondsSlider.Value,
            (int)WidgetRadiusSlider.Value);

    private int CountWidgetPendingChanges()
    {
        var n = 0;
        foreach (var (name, cb) in _peopleChecks)
        {
            var on = cb.IsChecked == true;
            if (on != _widgetBaselinePeople.Contains(name))
                n++;
        }
        foreach (var (key, cb) in _pairChecks)
        {
            var on = cb.IsChecked == true;
            if (on != _widgetBaselinePairs.Contains(key))
                n++;
        }
        var cur = ReadWidgetSettingsFromSliders();
        if (cur.Size != _widgetBaselineIni.Size) n++;
        if (cur.Seconds != _widgetBaselineIni.Seconds) n++;
        if (cur.Radius != _widgetBaselineIni.Radius) n++;
        return n;
    }

    private void UpdateWidgetPendingUi()
    {
        if (!IsLoaded)
            return;
        var n = CountWidgetPendingChanges();
        if (n == 0)
        {
            WidgetPendingStatus.Text = "No pending changes";
            WidgetApplyButton.IsEnabled = false;
        }
        else
        {
            WidgetPendingStatus.Text = n == 1 ? "1 change not applied" : $"{n} changes not applied";
            WidgetPendingStatus.Foreground = (System.Windows.Media.Brush)FindResource("BrushGold");
            WidgetApplyButton.IsEnabled = true;
        }
        if (n == 0)
            WidgetPendingStatus.Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted");
    }

    private void WireWidgetToggle(ToggleButton toggle)
    {
        void OnToggle(object? _, RoutedEventArgs __) => UpdateWidgetPendingUi();
        toggle.Checked += OnToggle;
        toggle.Unchecked += OnToggle;
    }

    private void WidgetPeopleFilter_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        RebuildWidgetPeopleChips();
        RebuildWidgetPairChips();
    }

    private bool PersonPassesFilter(PersonFolder person)
    {
        var q = WidgetPeopleSearch.Text?.Trim() ?? "";
        if (q.Length > 0 && person.Name.Contains(q, StringComparison.OrdinalIgnoreCase) == false)
            return false;
        if (WidgetFeaturedOnlyCheck.IsChecked == true)
        {
            var featured = _peopleChecks.TryGetValue(person.Name, out var t) && t.IsChecked == true;
            if (!featured)
                return false;
        }
        return true;
    }

    private void RebuildWidgetPeopleChips()
    {
        WidgetPeopleChecks.Children.Clear();
        foreach (var person in _widgetAllPeople)
        {
            if (!PersonPassesFilter(person))
                continue;
            var enabled = _peopleChecks.TryGetValue(person.Name, out var existing)
                ? existing.IsChecked == true
                : _widgetBaselinePeople.Contains(person.Name);
            var chip = UiControls.CreatePersonChip(person.Name, person.ThumbnailPath, enabled, out var toggle);
            _peopleChecks[person.Name] = toggle;
            WireWidgetToggle(toggle);
            WidgetPeopleChecks.Children.Add(chip);
        }
    }

    private void RebuildWidgetPairChips()
    {
        var q = WidgetPeopleSearch.Text?.Trim() ?? "";
        WidgetPairChecks.Children.Clear();
        var byName = _widgetAllPeople.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var other in _widgetAllPeople.Where(p =>
                     !string.Equals(p.Name, PhotoPaths.AugustPerson, StringComparison.Ordinal)))
        {
            var key = $"{PhotoPaths.AugustPerson} + {other.Name}";
            if (q.Length > 0 && key.Contains(q, StringComparison.OrdinalIgnoreCase) == false)
                continue;
            var on = _pairChecks.TryGetValue(key, out var existing)
                ? existing.IsChecked == true
                : _widgetBaselinePairs.Contains(key);
            byName.TryGetValue(PhotoPaths.AugustPerson, out var aug);
            var chip = UiControls.CreatePersonChip(key, other.ThumbnailPath ?? aug?.ThumbnailPath, on, out var toggle);
            _pairChecks[key] = toggle;
            WireWidgetToggle(toggle);
            WidgetPairChecks.Children.Add(chip);
        }
    }

    private void SaveFeaturedFromUi()
    {
        var model = _featured.ReadModel();
        model.People.Clear();
        foreach (var (name, cb) in _peopleChecks.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (cb.IsChecked == true)
                model.People.Add(name);
        }
        model.Pairs.Clear();
        foreach (var (key, cb) in _pairChecks)
        {
            if (cb.IsChecked == true)
                model.Pairs.Add($"pair: {key}");
        }
        _featured.WriteModel(model);
    }

    private async void ApplyWidgetChanges_OnClick(object sender, RoutedEventArgs e)
    {
        if (CountWidgetPendingChanges() == 0)
            return;
        try
        {
            SaveFeaturedFromUi();
            CommitWidgetIniWrite();
            await RunUpdateFeaturedScriptAsync();
            RainmeterService.RefreshPhotoWidget();
            CaptureWidgetBaseline();
            RebuildWidgetPeopleChips();
            RebuildWidgetPairChips();
            RefreshPhotoStrips();
            AppendLog("Widget changes applied.");
            ShowToast("Widget updated");
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private void NextWidgetPhoto_OnClick(object sender, RoutedEventArgs e)
    {
        RainmeterService.AdvancePhotoWidget();
        Dispatcher.BeginInvoke(RefreshCurrentWidgetPhoto, DispatcherPriority.Background);
    }

    private void RefreshPhotoStrips()
    {
        PinnedPhotosPanel.Children.Clear();
        HiddenPhotosPanel.Children.Clear();
        var model = _featured.ReadModel();
        foreach (var path in model.PinnedPhotos)
        {
            var p = path;
            PinnedPhotosPanel.Children.Add(UiControls.CreateWidgetThumbChip(
                Path.GetFileName(p),
                File.Exists(p) ? p : null,
                () => RemovePinnedPhoto(p)));
        }
        foreach (var ex in model.Excludes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var name = ex;
            var thumb = ResolveWidgetThumbForFileName(name);
            HiddenPhotosPanel.Children.Add(UiControls.CreateWidgetThumbChip(
                name,
                thumb,
                () => _ = RemoveHiddenPhotoAsync(name)));
        }
    }

    private static string? ResolveWidgetThumbForFileName(string fileName)
    {
        var featured = Path.Combine(PhotoPaths.WidgetFeatured, fileName);
        if (File.Exists(featured))
            return featured;
        var root = Directory.GetFiles(PhotoPaths.WidgetRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
        return root;
    }

    private void RemovePinnedPhoto(string fullPath)
    {
        _featured.RemovePinnedPhoto(fullPath);
        RefreshPhotoStrips();
        AppendLog($"Unpinned: {Path.GetFileName(fullPath)}");
        ShowToast("Pinned photo removed");
    }

    private async Task RemoveHiddenPhotoAsync(string fileName)
    {
        _featured.RemoveExcludeFileName(fileName);
        RefreshPhotoStrips();
        await RunUpdateFeaturedScriptAsync();
        RainmeterService.RefreshPhotoWidget();
        AppendLog($"Removed exclude: {fileName}");
        ShowToast($"Showing {fileName} again");
    }
}
