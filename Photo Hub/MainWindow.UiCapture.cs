using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoHub;

public partial class MainWindow
{
    public void CaptureUiScreenshots(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var pages = new[]
        {
            (PhotoHubPage.Sort, "sort"),
            (PhotoHubPage.People, "people"),
            (PhotoHubPage.Widget, "widget"),
            (PhotoHubPage.Photos, "photos"),
            (PhotoHubPage.IPhone, "iphone"),
            (PhotoHubPage.Find, "find"),
            (PhotoHubPage.Undo, "undo"),
            (PhotoHubPage.Log, "log"),
        };
        Width = 1180;
        Height = 800;
        foreach (var (page, name) in pages)
        {
            ShowPage(page);
            UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            Thread.Sleep(500);
            SavePng(this, Path.Combine(outputDir, $"tab-{name}.png"));
        }
    }

    private static void SavePng(Window window, string path)
    {
        window.UpdateLayout();
        var width = (int)Math.Max(window.ActualWidth, window.RenderSize.Width);
        var height = (int)Math.Max(window.ActualHeight, window.RenderSize.Height);
        if (width < 1 || height < 1)
        {
            width = 1180;
            height = 800;
            window.Width = width;
            window.Height = height;
            window.UpdateLayout();
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
