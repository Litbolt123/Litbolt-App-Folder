using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private void FindPhotos_OnClick(object sender, RoutedEventArgs e)
    {
        int? year = int.TryParse(FindYearBox.Text.Trim(), out var y) ? y : null;
        var hits = PhotoSearchService.Search(
            FindPersonBox.Text,
            FindPairBox.Text,
            year,
            FindFileBox.Text);
        FindResults.ItemsSource = hits;
        FindResultsGrid.Children.Clear();
        _findCards.Clear();
        foreach (var hit in hits)
        {
            var card = UiControls.CreateFindThumbCard(hit, SelectFindHit, _selectedFindHit?.Path == hit.Path);
            _findCards[hit.Path] = card;
            FindResultsGrid.Children.Add(card);
        }
        FindStatusText.Text = $"{hits.Count} result(s).";
    }

    private void SelectFindHit(PhotoHit hit)
    {
        _selectedFindHit = hit;
        foreach (var (path, card) in _findCards)
        {
            card.BorderBrush = new System.Windows.Media.SolidColorBrush(
                path == hit.Path ? LitboltTheme.Accent : LitboltTheme.ButtonBg);
        }
    }

    private void OpenFindResult_OnClick(object sender, RoutedEventArgs e)
    {
        var hit = _selectedFindHit ?? FindResults.SelectedItem as PhotoHit;
        if (hit is null)
            return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{hit.Path}\"",
            UseShellExecute = true,
        });
    }

    private List<FoldLogEntry> _undoEntries = [];
    private ICollectionView? _undoView;

    private void RefreshUndoList_OnClick(object sender, RoutedEventArgs e)
    {
        _undoEntries = FoldLogParser.ParseRecent(150).ToList();
        _undoView = CollectionViewSource.GetDefaultView(_undoEntries);
        _undoView.Filter = UndoFilter;
        UndoList.ItemsSource = _undoView;
        UpdateUndoSelectionStatus();
    }

    private bool UndoFilter(object item)
    {
        if (item is not FoldLogEntry entry)
            return false;
        var q = UndoSearchBox.Text?.Trim() ?? "";
        if (q.Length == 0)
            return true;
        return entry.Kind.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.Source.Contains(q, StringComparison.OrdinalIgnoreCase)
               || entry.Destination.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void UndoSearch_OnChanged(object sender, TextChangedEventArgs e) => _undoView?.Refresh();

    private void UndoList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateUndoSelectionStatus();

    private void UpdateUndoSelectionStatus()
    {
        if (!IsLoaded)
            return;
        UndoSelectionStatus.Text = UndoList.SelectedItem is FoldLogEntry entry
            ? $"{entry.Kind}: {Path.GetFileName(entry.Destination)}"
            : "Select a fold-log entry";
    }

    private void UndoSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (UndoList.SelectedItem is not FoldLogEntry entry)
        {
            MessageBox.Show("Select a log line to undo.", "Photo Hub", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (entry.Kind is not (FoldLogActionKind.Copy or FoldLogActionKind.Merge or FoldLogActionKind.Rename))
            return;
        var confirm = MessageBox.Show(
            $"Undo this {entry.Kind}?\n\n{entry.Source}\n→\n{entry.Destination}",
            "Confirm undo", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;
        var result = FoldLogUndoService.TryUndo(entry);
        AppendLog(result.Message, !result.Success);
        RefreshUndoList_OnClick(sender, e);
        if (result.Success)
            RefreshPeople();
    }
}
