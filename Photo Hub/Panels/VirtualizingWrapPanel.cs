using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PhotoHub.Panels;

/// <summary>Fixed-size virtualizing wrap panel for the People grid.</summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(156.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
            new FrameworkPropertyMetadata(172.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    private readonly Dictionary<int, Rect> _layout = new();
    private double _offsetY;
    private double _viewportWidth;
    private double _viewportHeight;
    private int _itemsPerLine = 1;
    private int _lineCount;

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _itemsPerLine * ItemWidth;
    public double ExtentHeight => _lineCount * ItemHeight;
    public double ViewportWidth => _viewportWidth;
    public double ViewportHeight => _viewportHeight;
    public double HorizontalOffset => 0;
    public double VerticalOffset => _offsetY;
    public ScrollViewer ScrollOwner { get; set; } = null!;

    public void LineUp() => SetVerticalOffset(_offsetY - ItemHeight);
    public void LineDown() => SetVerticalOffset(_offsetY + ItemHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageUp() => SetVerticalOffset(_offsetY - _viewportHeight);
    public void PageDown() => SetVerticalOffset(_offsetY + _viewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelUp() => LineUp();
    public void MouseWheelDown() => LineDown();
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is not UIElement child)
            return rectangle;
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null)
            return rectangle;
        var index = owner.ItemContainerGenerator.IndexFromContainer(child);
        if (index < 0)
            return rectangle;
        var y = index / _itemsPerLine * ItemHeight;
        if (y < _offsetY)
            SetVerticalOffset(y);
        else if (y + ItemHeight > _offsetY + _viewportHeight)
            SetVerticalOffset(y + ItemHeight - _viewportHeight);
        return rectangle;
    }

    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        offset = Math.Max(0, Math.Min(offset, Math.Max(0, ExtentHeight - _viewportHeight)));
        if (Math.Abs(offset - _offsetY) < 0.5)
            return;
        _offsetY = offset;
        InvalidateMeasure();
        ScrollOwner?.InvalidateScrollInfo();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var count = owner?.Items.Count ?? 0;
        _viewportWidth = double.IsInfinity(availableSize.Width) ? Math.Max(_viewportWidth, 400) : availableSize.Width;
        _viewportHeight = double.IsInfinity(availableSize.Height) ? Math.Max(_viewportHeight, 400) : availableSize.Height;
        _itemsPerLine = Math.Max(1, (int)Math.Floor(_viewportWidth / ItemWidth));
        _lineCount = count == 0 ? 0 : (count + _itemsPerLine - 1) / _itemsPerLine;

        _layout.Clear();
        if (owner is null || count == 0)
        {
            RemoveAllChildren(owner);
            return new Size(_viewportWidth, 0);
        }

        IItemContainerGenerator generator = owner.ItemContainerGenerator;
        var firstLine = Math.Max(0, (int)(_offsetY / ItemHeight));
        var lastLine = (int)Math.Ceiling((_offsetY + _viewportHeight) / ItemHeight);
        var firstIndex = firstLine * _itemsPerLine;
        var lastIndex = Math.Min(count - 1, (lastLine + 1) * _itemsPerLine - 1);

        var startPos = generator.GeneratorPositionFromIndex(firstIndex);
        using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
        {
            for (var i = firstIndex; i <= lastIndex; i++)
            {
                var isNew = false;
                var child = generator.GenerateNext(out isNew) as UIElement;
                if (child is null)
                    continue;
                if (isNew)
                    AddInternalChild(child);
                generator.PrepareItemContainer(child);
                child.Measure(new Size(ItemWidth, ItemHeight));
                var row = i / _itemsPerLine;
                var col = i % _itemsPerLine;
                _layout[i] = new Rect(col * ItemWidth, row * ItemHeight, ItemWidth, ItemHeight);
            }
        }

        RemoveInvisible(owner, firstIndex, lastIndex);
        ScrollOwner?.InvalidateScrollInfo();
        return new Size(_itemsPerLine * ItemWidth, _lineCount * ItemHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null)
            return finalSize;
        var generator = owner.ItemContainerGenerator;
        foreach (UIElement child in InternalChildren)
        {
            var index = generator.IndexFromContainer(child);
            if (index < 0 || !_layout.TryGetValue(index, out var rect))
                continue;
            child.Arrange(new Rect(rect.X, rect.Y - _offsetY, rect.Width, rect.Height));
        }

        return finalSize;
    }

    private void RemoveInvisible(ItemsControl owner, int minIndex, int maxIndex)
    {
        var containers = owner.ItemContainerGenerator;
        IItemContainerGenerator generator = containers;
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var child = InternalChildren[i];
            var index = containers.IndexFromContainer(child);
            if (index < minIndex || index > maxIndex)
            {
                generator.Remove(new GeneratorPosition(index, 0), 1);
                RemoveInternalChildRange(i, 1);
            }
        }
    }

    private void RemoveAllChildren(ItemsControl? owner)
    {
        if (owner is null)
            return;
        var containers = owner.ItemContainerGenerator;
        IItemContainerGenerator generator = containers;
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var child = InternalChildren[i];
            var index = containers.IndexFromContainer(child);
            if (index >= 0)
                generator.Remove(new GeneratorPosition(index, 0), 1);
            RemoveInternalChildRange(i, 1);
        }
    }
}
