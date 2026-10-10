using System.Diagnostics;
using System.Windows;
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
        FindStatusText.Text = $"{hits.Count} result(s).";
    }

    private void OpenFindResult_OnClick(object sender, RoutedEventArgs e)
    {
        if (FindResults.SelectedItem is not PhotoHit hit)
            return;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{hit.Path}\"",
            UseShellExecute = true,
        });
    }

    private void RefreshUndoList_OnClick(object sender, RoutedEventArgs e) =>
        UndoList.ItemsSource = FoldLogParser.ParseRecent(150);

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
