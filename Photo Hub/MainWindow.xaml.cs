using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Litbolt.Shell;
using Litbolt.Shell.Tray;
using Microsoft.Win32;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow : Window, ITrayHost
{
    private readonly ProcessRunner _runner = new();
    private readonly FeaturedPeopleService _featured = new();
    private readonly Dictionary<string, CheckBox> _peopleChecks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _pairChecks = new(StringComparer.Ordinal);
    private CancellationTokenSource? _sortCts;
    private readonly AppTrayIcon _tray;
    private readonly ActivateSignalWatcher _activateWatcher;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = TrayIconAssets.CreateWindowIcon(); } catch { /* ignore */ }

        AppTrayIcon? tray = null;
        tray = new AppTrayIcon("Photo Hub", menu =>
        {
            menu.Items.Add(TrayMenuTheme.CreateHeader("Photo Hub"));
            menu.Items.Add("Open Photo Hub", null, (_, _) => tray!.ShowMainWindow());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => tray!.RequestQuit());
        });
        _tray = tray;
        _tray.Attach(this);
        _activateWatcher = new ActivateSignalWatcher(App.DataDirectory, ActivateFromSecondInstance);

        SourceFolderBox.Text = PhotoPaths.IPhoneBackup;
        _runner.LineReceived += OnRunnerLine;
        _runner.ErrorReceived += s => AppendLog(s, true);

        Loaded += (_, _) =>
        {
            _tray.Show();
            _activateWatcher.Start();
            IPhoneBackupBox.Text = PhotoPaths.IPhoneBackup;
            RefreshPeople();
            BuildWidgetChecks();
            InitWidgetAndPhotos();
            UpdateBackupStatus();
            RefreshUndoList_OnClick(this, new RoutedEventArgs());
        };
        MainTabs.SelectionChanged += (_, _) =>
        {
            if (MainTabs.SelectedItem == PeopleTab)
                RefreshPeople();
            if (MainTabs.SelectedItem == WidgetTab)
            {
                BuildWidgetChecks();
                RefreshExcludeList();
            }
            if (MainTabs.SelectedItem == IPhoneTab)
                UpdateBackupStatus();
            if (MainTabs.SelectedItem == UndoTab)
                RefreshUndoList_OnClick(this, new RoutedEventArgs());
        };
    }

    public bool ShouldMinimizeToTrayOnClose => false;

    public void RequestForceClose() => _forceClose = true;

    public void OnTrayPreferenceChanged() => _tray.Show();

    private void ActivateFromSecondInstance() => Dispatcher.Invoke(() => _tray.ShowMainWindow());

    private void AppendLog(string message, bool isError = false)
    {
        var prefix = isError ? "Error: " : "";
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss} {prefix}{message}\n");
        LogBox.ScrollToEnd();
    }

    private void OnRunnerLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeEl))
            {
                var type = typeEl.GetString();
                switch (type)
                {
                    case "log":
                        AppendLog(root.GetProperty("message").GetString() ?? line,
                            root.TryGetProperty("level", out var lv) && lv.GetString() == "error");
                        return;
                    case "progress":
                        var cur = root.GetProperty("current").GetInt32();
                        var tot = root.GetProperty("total").GetInt32();
                        var pct = tot > 0 ? 100.0 * cur / tot : 0;
                        Dispatcher.Invoke(() =>
                        {
                            SortProgress.Value = pct;
                            SortProgressText.Text = $"{cur} / {tot}";
                        });
                        return;
                    case "match":
                        AppendLog($"Matched {root.GetProperty("person").GetString()} — {Path.GetFileName(root.GetProperty("photo").GetString())}");
                        return;
                    case "unknown":
                        Dispatcher.Invoke(() =>
                        {
                            var vm = new UnknownFaceVm
                            {
                                Photo = root.GetProperty("photo").GetString() ?? "",
                                Crop = root.GetProperty("crop").GetString() ?? "",
                                Hint = $"{root.GetProperty("best").GetString()} ({root.GetProperty("score").GetDouble():F2})",
                            };
                            UnknownFacesPanel.Children.Add(new UnknownFaceCard(vm, AssignUnknown_OnClick));
                        });
                        return;
                    case "done":
                        AppendLog($"Done. Copied matches for {root.GetProperty("copied").GetInt32()} faces; {root.GetProperty("unknown_faces").GetInt32()} unknown.");
                        Dispatcher.Invoke(() => { SortProgress.Value = 100; RunSortBtn.IsEnabled = true; });
                        return;
                    case "error":
                        AppendLog(root.GetProperty("message").GetString() ?? "Unknown error", true);
                        return;
                }
            }
        }
        catch (JsonException)
        {
            /* plain text */
        }

        AppendLog(line);
    }

    private void BrowseSource_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder with new photos" };
        if (dlg.ShowDialog() == true)
            SourceFolderBox.Text = dlg.FolderName;
    }

    private async void RunSort_OnClick(object sender, RoutedEventArgs e)
    {
        var folder = SourceFolderBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            MessageBox.Show("That folder does not exist.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _sortCts?.Cancel();
        _sortCts = new CancellationTokenSource();
        UnknownFacesPanel.Children.Clear();
        RunSortBtn.IsEnabled = false;
        SortProgress.Value = 0;
        AppendLog($"Starting sort on {folder}…");

        try
        {
            var code = await _runner.RunPythonAsync(
                PhotoPaths.SortScript,
                ["sort", folder],
                _sortCts.Token);
            if (code != 0)
                AppendLog($"Sort finished with exit code {code}.", true);
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
        finally
        {
            RunSortBtn.IsEnabled = true;
        }
    }

    private async void AssignUnknown_OnClick(UnknownFaceVm vm, string personName)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            MessageBox.Show("Enter a person name.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _runner.RunPythonAsync(
                PhotoPaths.SortScript,
                ["assign", "--photo", vm.Photo, "--crop", vm.Crop, "--person", personName.Trim()],
                CancellationToken.None);
            for (var i = UnknownFacesPanel.Children.Count - 1; i >= 0; i--)
            {
                if (UnknownFacesPanel.Children[i] is UnknownFaceCard card && card.Tag == vm)
                    UnknownFacesPanel.Children.RemoveAt(i);
            }
            RefreshPeople();
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message, true);
        }
    }

    private void RefreshPeople_OnClick(object sender, RoutedEventArgs e) => RefreshPeople();

    private void RefreshPeople()
    {
        PeopleList.ItemsSource = FaceGroupsService.ListPeople();
    }

    private void RenamePerson_OnClick(object sender, RoutedEventArgs e)
    {
        if (PeopleList.SelectedItem is not PersonFolder person)
        {
            MessageBox.Show("Select a person folder first.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new InputDialog("Rename person", "New folder name (must match widget spelling):", person.Name)
        { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Value))
            return;

        if (MessageBox.Show($"Rename \"{person.Name}\" to \"{dlg.Value}\"?\n\nLogged to _fold-log.txt for undo.",
                "Confirm rename", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            FaceGroupsService.RenamePerson(person.Path, dlg.Value);
            AppendLog($"Renamed {person.Name} → {dlg.Value}");
            RefreshPeople();
            BuildWidgetChecks();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Rename failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MergePerson_OnClick(object sender, RoutedEventArgs e)
    {
        if (PeopleList.SelectedItem is not PersonFolder from)
        {
            MessageBox.Show("Select the folder to merge away.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var others = FaceGroupsService.ListPeople().Where(p => p.Path != from.Path).Select(p => p.Name).ToList();
        var pick = new PickPersonDialog("Merge into which person?", others) { Owner = this };
        if (pick.ShowDialog() != true || pick.Selected is not string intoName)
            return;

        var into = FaceGroupsService.ListPeople().First(p => p.Name == intoName);
        if (MessageBox.Show(
                $"Copy all files from \"{from.Name}\" into \"{into.Name}\"?\n\nNothing is deleted; merge is logged in _fold-log.txt.",
                "Confirm merge", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            FaceGroupsService.MergePeople(from.Path, into.Path);
            AppendLog($"Merged {from.Name} into {into.Name} (copies only).");
            RefreshPeople();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Merge failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BuildWidgetChecks()
    {
        var people = FaceGroupsService.ListPeople().Select(p => p.Name).ToList();
        var enabled = _featured.EnabledPeople();
        var pairsOn = _featured.EnabledPairs();

        WidgetPeopleChecks.Items.Clear();
        _peopleChecks.Clear();
        foreach (var name in people)
        {
            var cb = new CheckBox
            {
                Content = name,
                IsChecked = enabled.Contains(name),
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 4, 16, 4),
            };
            _peopleChecks[name] = cb;
            WidgetPeopleChecks.Items.Add(cb);
        }

        WidgetPairChecks.Items.Clear();
        _pairChecks.Clear();
        foreach (var other in people.Where(p => !string.Equals(p, PhotoPaths.AugustPerson, StringComparison.Ordinal)))
        {
            var key = $"{PhotoPaths.AugustPerson} + {other}";
            var cb = new CheckBox
            {
                Content = key,
                IsChecked = pairsOn.Contains(key),
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 4, 16, 4),
            };
            _pairChecks[key] = cb;
            WidgetPairChecks.Items.Add(cb);
        }
    }

    private void SaveFeatured_OnClick(object sender, RoutedEventArgs e)
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
        AppendLog("Saved featured-people.txt");
        RefreshExcludeList();
    }

    private void AddWidgetPhoto_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.heic;*.webp;*.bmp",
            Title = "Photo to always feature",
        };
        if (dlg.ShowDialog() != true)
            return;
        _featured.AddPhotoLine(dlg.FileName);
        AppendLog($"Added photo line: {dlg.FileName}");
        RefreshExcludeList();
    }

    private void BrowseIPhoneBackup_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "iPhone backup folder" };
        if (dlg.ShowDialog() == true)
            IPhoneBackupBox.Text = dlg.FolderName;
    }

    private async void UpdateWidget_OnClick(object sender, RoutedEventArgs e)
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

    private void ClearLog_OnClick(object sender, RoutedEventArgs e) => LogBox.Clear();

    protected override void OnClosed(EventArgs e)
    {
        _sortCts?.Cancel();
        _tray.Dispose();
        _activateWatcher.Dispose();
        base.OnClosed(e);
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_forceClose && _sortCts is { IsCancellationRequested: false } && RunSortBtn.IsEnabled == false)
        {
            if (MessageBox.Show("A sort is still running. Close anyway?", "Photo Hub",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _sortCts.Cancel();
        }
    }
}

internal sealed class UnknownFaceVm
{
    public required string Photo { get; init; }
    public required string Crop { get; init; }
    public required string Hint { get; init; }
}

internal sealed class UnknownFaceCard : Border
{
    public UnknownFaceCard(UnknownFaceVm vm, Action<UnknownFaceVm, string> onAssign)
    {
        Tag = vm;
        Width = 200;
        Margin = new Thickness(0, 0, 12, 12);
        Padding = new Thickness(8);
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0x1A, 0x22));
        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x30, 0x40));
        BorderThickness = new Thickness(1);

        var stack = new StackPanel();
        if (File.Exists(vm.Crop))
        {
            var img = new Image
            {
                Width = 176,
                Height = 176,
                Stretch = System.Windows.Media.Stretch.UniformToFill,
                Source = LoadThumb(vm.Crop),
            };
            stack.Children.Add(img);
        }

        stack.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(vm.Photo),
            Foreground = System.Windows.Media.Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 6, 0, 2),
        });
        stack.Children.Add(new TextBlock
        {
            Text = vm.Hint,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x90, 0xA0)),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 6),
        });

        var nameBox = LitboltTheme.MakeTextBox();
        var btn = LitboltTheme.MakeButton("Assign", primary: true);
        btn.Click += (_, _) => onAssign(vm, nameBox.Text);
        stack.Children.Add(nameBox);
        stack.Children.Add(btn);
        Child = stack;
    }

    private static BitmapImage? LoadThumb(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = 200;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch
        {
            return null;
        }
    }
}
