using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoHub.Services;

public static class UiControls
{
    public static BitmapImage? LoadThumb(string? path, int decodeWidth = 160)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = decodeWidth;
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

    public static Border CreatePersonChip(string name, string? thumbPath, bool isOn, out ToggleButton toggle)
    {
        toggle = new ToggleButton
        {
            IsChecked = isOn,
            Visibility = Visibility.Collapsed,
        };
        var face = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Background = new SolidColorBrush(LitboltTheme.Panel),
            Child = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = LoadThumb(thumbPath, 72),
            },
        };
        var label = new TextBlock
        {
            Text = name,
            FontFamily = Application.Current.TryFindResource("UiFont") as FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = 12,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0),
            MaxWidth = 140,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(face);
        stack.Children.Add(label);

        var border = new Border
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8, 6, 12, 6),
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(LitboltTheme.ButtonBg),
            Background = new SolidColorBrush(LitboltTheme.Panel),
            Cursor = Cursors.Hand,
            Tag = toggle,
        };
        var btn = toggle;
        void RefreshVisual()
        {
            var on = btn.IsChecked == true;
            border.BorderBrush = new SolidColorBrush(on ? LitboltTheme.Accent : LitboltTheme.ButtonBg);
            border.Background = new SolidColorBrush(on ? Color.FromRgb(0x3D, 0x34, 0x28) : LitboltTheme.Panel);
        }
        RefreshVisual();
        border.MouseLeftButtonUp += (_, _) =>
        {
            btn.IsChecked = btn.IsChecked != true;
            RefreshVisual();
        };
        btn.Checked += (_, _) => RefreshVisual();
        btn.Unchecked += (_, _) => RefreshVisual();
        var host = new Grid();
        host.Children.Add(stack);
        host.Children.Add(btn);
        border.Child = host;
        return border;
    }

    public static Border CreatePeopleGridCard(
        PersonFolder person,
        Action<PersonFolder> onSelect,
        Action<PersonFolder> onRename,
        Action<PersonFolder> onMerge)
    {
        var thumb = LoadThumb(person.ThumbnailPath, 120);
        var img = new Image
        {
            Width = 88,
            Height = 88,
            Stretch = Stretch.UniformToFill,
            Source = thumb,
        };
        var faceClip = new Border
        {
            Width = 88,
            Height = 88,
            CornerRadius = new CornerRadius(44),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = img,
        };
        var name = new TextBlock
        {
            Text = person.Name,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 120,
            FontSize = 12,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 8, 0, 2),
        };
        var count = new TextBlock
        {
            Text = $"{person.PhotoCount} photos",
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Foreground = new SolidColorBrush(LitboltTheme.Muted),
        };
        var overlay = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var rename = LitboltTheme.MakeButton("Rename", primary: false);
        rename.Padding = new Thickness(10, 6, 10, 6);
        rename.Margin = new Thickness(0, 0, 4, 0);
        rename.Click += (_, _) => onRename(person);
        var merge = LitboltTheme.MakeButton("Merge", primary: false);
        merge.Padding = new Thickness(10, 6, 10, 6);
        merge.Click += (_, _) => onMerge(person);
        overlay.Children.Add(rename);
        overlay.Children.Add(merge);

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(faceClip);
        stack.Children.Add(name);
        stack.Children.Add(count);
        stack.Children.Add(overlay);

        var card = new Border
        {
            Width = 140,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(LitboltTheme.Panel),
            BorderBrush = new SolidColorBrush(LitboltTheme.ButtonBg),
            BorderThickness = new Thickness(1),
            Child = stack,
            Tag = person,
            Cursor = Cursors.Hand,
        };
        card.MouseEnter += (_, _) => overlay.Visibility = Visibility.Visible;
        card.MouseLeave += (_, _) => overlay.Visibility = Visibility.Collapsed;
        card.MouseLeftButtonUp += (_, _) => onSelect(person);
        return card;
    }

    public static Border CreateFindThumbCard(PhotoHit hit, Action<PhotoHit> onSelect, bool selected)
    {
        var img = new Image
        {
            Stretch = Stretch.UniformToFill,
            Source = LoadThumb(hit.Path, 280),
        };
        var zoomHost = new Border
        {
            Height = 160,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = img,
        };
        var scale = new ScaleTransform(1, 1);
        zoomHost.RenderTransformOrigin = new Point(0.5, 0.5);
        zoomHost.RenderTransform = scale;
        zoomHost.MouseEnter += (_, _) => scale.ScaleX = scale.ScaleY = 1.08;
        zoomHost.MouseLeave += (_, _) => scale.ScaleX = scale.ScaleY = 1.0;

        var cap = new TextBlock
        {
            Text = $"{hit.Person} · {hit.FileName}",
            FontSize = 11,
            Foreground = new SolidColorBrush(LitboltTheme.Muted),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var stack = new StackPanel();
        stack.Children.Add(zoomHost);
        stack.Children.Add(cap);

        var card = new Border
        {
            Width = 200,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(LitboltTheme.Panel),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(selected ? LitboltTheme.Accent : LitboltTheme.ButtonBg),
            Child = stack,
            Tag = hit,
            Cursor = Cursors.Hand,
        };
        card.MouseLeftButtonUp += (_, _) => onSelect(hit);
        return card;
    }
}
