using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PhotoHub.Services;

namespace PhotoHub;

public partial class MainWindow
{
    private PhotoHit? _selectedFindHit;
    private PersonFolder? _selectedPerson;
    private readonly Dictionary<string, Border> _findCards = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _toastTimer;
    private bool _suppressNav;

    private void InitNavigationUi()
    {
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        };
        NavSort.IsChecked = true;
        ShowPage(PhotoHubPage.Sort);
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressNav)
            return;
        if (sender is not RadioButton rb || rb.IsChecked != true)
            return;
        ShowPage(rb.Name switch
        {
            nameof(NavSort) => PhotoHubPage.Sort,
            nameof(NavPeople) => PhotoHubPage.People,
            nameof(NavWidget) => PhotoHubPage.Widget,
            nameof(NavPhotos) => PhotoHubPage.Photos,
            nameof(NavIPhone) => PhotoHubPage.IPhone,
            nameof(NavFind) => PhotoHubPage.Find,
            nameof(NavUndo) => PhotoHubPage.Undo,
            nameof(NavLog) => PhotoHubPage.Log,
            _ => PhotoHubPage.Sort,
        });
    }

    internal void ShowPage(PhotoHubPage page)
    {
        _suppressNav = true;
        NavSort.IsChecked = page == PhotoHubPage.Sort;
        NavPeople.IsChecked = page == PhotoHubPage.People;
        NavWidget.IsChecked = page == PhotoHubPage.Widget;
        NavPhotos.IsChecked = page == PhotoHubPage.Photos;
        NavIPhone.IsChecked = page == PhotoHubPage.IPhone;
        NavFind.IsChecked = page == PhotoHubPage.Find;
        NavUndo.IsChecked = page == PhotoHubPage.Undo;
        NavLog.IsChecked = page == PhotoHubPage.Log;
        _suppressNav = false;

        TabScrollHost.Visibility = page is PhotoHubPage.People or PhotoHubPage.Widget
            ? Visibility.Collapsed
            : Visibility.Visible;
        PageSort.Visibility = page == PhotoHubPage.Sort ? Visibility.Visible : Visibility.Collapsed;
        PagePeople.Visibility = page == PhotoHubPage.People ? Visibility.Visible : Visibility.Collapsed;
        PageWidget.Visibility = page == PhotoHubPage.Widget ? Visibility.Visible : Visibility.Collapsed;
        PagePhotos.Visibility = page == PhotoHubPage.Photos ? Visibility.Visible : Visibility.Collapsed;
        PageIPhone.Visibility = page == PhotoHubPage.IPhone ? Visibility.Visible : Visibility.Collapsed;
        PageFind.Visibility = page == PhotoHubPage.Find ? Visibility.Visible : Visibility.Collapsed;
        PageUndo.Visibility = page == PhotoHubPage.Undo ? Visibility.Visible : Visibility.Collapsed;
        PageLog.Visibility = page == PhotoHubPage.Log ? Visibility.Visible : Visibility.Collapsed;

        if (page == PhotoHubPage.People)
            RefreshPeople();
        if (page == PhotoHubPage.Widget)
        {
            BuildWidgetChecks();
            RefreshPhotoStrips();
            RefreshWidgetPreview();
            UpdateWidgetPendingUi();
        }
        if (page == PhotoHubPage.IPhone)
            UpdateBackupStatus();
        if (page == PhotoHubPage.Undo)
            RefreshUndoList_OnClick(this, new RoutedEventArgs());
    }

    public void ShowToast(string message, bool isError = false)
    {
        ToastText.Text = message;
        ToastBorder.Background = isError
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x28, 0x28))
            : (System.Windows.Media.Brush)FindResource("BrushGoldDim");
        ToastText.Foreground = isError
            ? System.Windows.Media.Brushes.White
            : (System.Windows.Media.Brush)FindResource("BrushGold");
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer?.Stop();
        _toastTimer?.Start();
    }

    private void SetGlobalProgress(double value, bool visible)
    {
        GlobalProgress.Value = value;
        GlobalProgress.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    internal enum PhotoHubPage
    {
        Sort, People, Widget, Photos, IPhone, Find, Undo, Log,
    }
}
