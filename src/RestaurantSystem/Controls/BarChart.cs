using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RestaurantSystem.Controls;

/// <summary>
/// Стовпчикова діаграма з градієнтними стовпцями, сіткою та інтерактивною підказкою.
/// Повністю малюється вручну (без сторонніх бібліотек).
/// </summary>
public class BarChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> HighlightLastProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(HighlightLast), true);

    public static readonly StyledProperty<bool> ShowAxisProperty =
        AvaloniaProperty.Register<BarChart, bool>(nameof(ShowAxis), true);

    private static readonly IBrush BarBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#FFC977"), 0), new GradientStop(Color.Parse("#E4483A"), 1) }
    }.ToImmutable();

    private static readonly IBrush IdleBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#5A4636"), 0), new GradientStop(Color.Parse("#3A2E25"), 1) }
    }.ToImmutable();

    private static readonly IBrush LabelBrush = new ImmutableSolidColorBrush(Color.Parse("#74675C"));
    private static readonly IBrush TooltipBg = new ImmutableSolidColorBrush(Color.Parse("#F6EEE6"));
    private static readonly IBrush TooltipFg = new ImmutableSolidColorBrush(Color.Parse("#1A120C"));
    private static readonly IPen GridPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#2E2620")), 1,
        new ImmutableDashStyle(new double[] { 3, 4 }, 0));

    private int _hover = -1;

    static BarChart()
    {
        AffectsRender<BarChart>(PointsProperty, HighlightLastProperty, ShowAxisProperty);
    }

    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public bool HighlightLast
    {
        get => GetValue(HighlightLastProperty);
        set => SetValue(HighlightLastProperty, value);
    }

    public bool ShowAxis
    {
        get => GetValue(ShowAxisProperty);
        set => SetValue(ShowAxisProperty, value);
    }

    private Rect PlotArea => new(
        ShowAxis ? 44 : 4, 34,
        Math.Max(0, Bounds.Width - (ShowAxis ? 48 : 8)),
        Math.Max(0, Bounds.Height - 34 - 24));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var points = Points;
        if (points is null || points.Count == 0) return;

        var plot = PlotArea;
        var x = e.GetPosition(this).X - plot.X;
        var index = x < 0 || x > plot.Width ? -1 : (int)(x / (plot.Width / points.Count));
        index = Math.Clamp(index, -1, points.Count - 1);
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var points = Points;
        if (points is null || points.Count == 0 || Bounds.Width < 40 || Bounds.Height < 60)
            return;

        var plot = PlotArea;
        var (max, lines) = ChartText.NiceScale(points.Max(p => p.Value));

        // Сітка та підписи осі Y
        for (var i = 0; i <= lines; i++)
        {
            var y = plot.Bottom - plot.Height * i / lines;
            ctx.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            if (ShowAxis)
            {
                var label = ChartText.Make(ChartText.Compact(max * i / lines), 10.5, LabelBrush);
                ctx.DrawText(label, new Point(plot.Left - label.Width - 8, y - label.Height / 2));
            }
        }

        var slot = plot.Width / points.Count;
        var barWidth = Math.Clamp(slot * 0.58, 3, 38);
        var labelEvery = Math.Max(1, (int)Math.Ceiling(points.Count / (plot.Width / 46)));
        var active = _hover >= 0 ? _hover : HighlightLast ? points.Count - 1 : -1;

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var h = max <= 0 ? 0 : plot.Height * p.Value / max;
            var cx = plot.Left + slot * i + slot / 2;
            var rect = new Rect(cx - barWidth / 2, plot.Bottom - h, barWidth, Math.Max(h, 2));
            var radius = Math.Min(barWidth / 2, 7);
            ctx.DrawRectangle(i == active ? BarBrush : IdleBrush, null,
                new RoundedRect(rect, radius, radius, 2, 2));

            if (i % labelEvery == 0 || i == points.Count - 1)
            {
                var label = ChartText.Make(p.Label, 10.5, i == active ? TooltipBg : LabelBrush,
                    i == active ? FontWeight.Bold : FontWeight.Normal);
                ctx.DrawText(label, new Point(cx - label.Width / 2, plot.Bottom + 7));
            }
        }

        // Підказка над активним стовпцем
        if (active >= 0)
        {
            var p = points[active];
            var h = max <= 0 ? 0 : plot.Height * p.Value / max;
            var cx = plot.Left + slot * active + slot / 2;
            var text = ChartText.Make(p.Tooltip, 11.5, TooltipFg, FontWeight.Bold);
            var w = text.Width + 18;
            var bx = Math.Clamp(cx - w / 2, 0, Bounds.Width - w);
            var by = Math.Max(0, plot.Bottom - h - 32);
            ctx.DrawRectangle(TooltipBg, null, new RoundedRect(new Rect(bx, by, w, 24), 8));
            ctx.DrawText(text, new Point(bx + 9, by + 12 - text.Height / 2));
        }
    }
}
