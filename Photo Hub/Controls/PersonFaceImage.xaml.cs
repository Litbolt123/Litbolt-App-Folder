using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PhotoHub.Services;

namespace PhotoHub.Controls;

public partial class PersonFaceImage : UserControl
{
    private CancellationTokenSource? _loadCts;

    public static readonly DependencyProperty SourcePathProperty =
        DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(PersonFaceImage),
            new PropertyMetadata(null, OnSourcePathChanged));

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public PersonFaceImage()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        };
    }

    private static void OnSourcePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PersonFaceImage img)
            img.StartLoad(e.NewValue as string);
    }

    private void StartLoad(string? path)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        FaceImg.Source = null;

        if (string.IsNullOrEmpty(path))
            return;

        _ = LoadAsync(path, token);
    }

    private async Task LoadAsync(string path, CancellationToken token)
    {
        try
        {
            var bmp = await ThumbnailCache.Shared.GetAsync(path, 160, token);
            if (token.IsCancellationRequested || bmp is null)
                return;
            FaceImg.Source = bmp;
        }
        catch (OperationCanceledException)
        {
            // recycled container
        }
    }
}
