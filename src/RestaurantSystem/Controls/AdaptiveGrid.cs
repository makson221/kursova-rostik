using Avalonia;
using Avalonia.Controls;

namespace RestaurantSystem.Controls;

/// <summary>
/// Адаптивна сітка: розміщує елементи у стільки колонок, скільки вміщається
/// при мінімальній ширині <see cref="MinItemWidth"/>, і розтягує їх на всю ширину.
/// </summary>
public class AdaptiveGrid : Panel
{
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<AdaptiveGrid, double>(nameof(MinItemWidth), 200);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AdaptiveGrid, double>(nameof(Spacing), 12);

    static AdaptiveGrid()
    {
        AffectsMeasure<AdaptiveGrid>(MinItemWidthProperty, SpacingProperty);
    }

    public double MinItemWidth
    {
        get => GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private (int Columns, double ItemWidth) Layout(double width)
    {
        if (double.IsInfinity(width) || width <= 0)
            width = MinItemWidth * 3 + Spacing * 2;
        var columns = Math.Max(1, (int)((width + Spacing) / (MinItemWidth + Spacing)));
        var itemWidth = (width - Spacing * (columns - 1)) / columns;
        return (columns, itemWidth);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, itemWidth) = Layout(availableSize.Width);
        double height = 0, rowHeight = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if (i % columns == columns - 1 || i == Children.Count - 1)
            {
                height += rowHeight + (i < Children.Count - 1 ? Spacing : 0);
                rowHeight = 0;
            }
        }
        var width = double.IsInfinity(availableSize.Width) ? columns * itemWidth + (columns - 1) * Spacing : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, itemWidth) = Layout(finalSize.Width);
        double y = 0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            var end = Math.Min(start + columns, Children.Count);
            var rowHeight = 0.0;
            for (var i = start; i < end; i++)
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            for (var i = start; i < end; i++)
                Children[i].Arrange(new Rect((i - start) * (itemWidth + Spacing), y, itemWidth, rowHeight));
            y += rowHeight + Spacing;
        }
        return finalSize;
    }
}
