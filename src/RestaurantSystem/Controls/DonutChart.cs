using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RestaurantSystem.Controls;

/// <summary>Кільцева діаграма часток із підсвічуванням сегмента під курсором.</summary>
public class DonutChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<DonutSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<DonutSegment>?>(nameof(Segments));

    public static readonly StyledProperty<string> CenterTitleProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(CenterTitle), string.Empty);

    public static readonly StyledProperty<string> CenterValueProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(CenterValue), string.Empty);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<DonutChart, double>(nameof(Thickness), 20);

    private static readonly IBrush TitleBrush = new ImmutableSolidColorBrush(Color.Parse("#A89A8D"));
    private static readonly IBrush ValueBrush = new ImmutableSolidColorBrush(Color.Parse("#F6EEE6"));
    private static readonly IBrush TrackBrush = new ImmutableSolidColorBrush(Color.Parse("#27201B"));

    private int _hover = -1;

    static DonutChart()
    {
        AffectsRender<DonutChart>(SegmentsProperty, CenterTitleProperty, CenterValueProperty, ThicknessProperty);
    }

    public IReadOnlyList<DonutSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string CenterTitle
    {
        get => GetValue(CenterTitleProperty);
        set => SetValue(CenterTitleProperty, value);
    }

    public string CenterValue
    {
        get => GetValue(CenterValueProperty);
        set => SetValue(CenterValueProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    private (Point Center, double Outer, double Inner) Geometry()
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var outer = size / 2 - 6;
        return (new Point(Bounds.Width / 2, Bounds.Height / 2), outer, outer - Thickness);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var segments = Segments;
        if (segments is null || segments.Count == 0) return;

        var (c, outer, inner) = Geometry();
        var p = e.GetPosition(this) - c;
        var dist = Math.Sqrt(p.X * p.X + p.Y * p.Y);
        var index = -1;
        if (dist >= inner - 4 && dist <= outer + 6)
        {
            var angle = Math.Atan2(p.Y, p.X) + Math.PI / 2;
            if (angle < 0) angle += Math.PI * 2;
            var total = segments.Sum(s => s.Value);
            var acc = 0.0;
            for (var i = 0; i < segments.Count; i++)
            {
                acc += segments[i].Value / total * Math.PI * 2;
                if (angle <= acc) { index = i; break; }
            }
        }

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
        var (c, outer, inner) = Geometry();
        if (outer <= Thickness) return;

        ctx.DrawGeometry(TrackBrush, null, Ring(c, outer, inner, 0, Math.PI * 2 - 0.0001));

        var segments = Segments;
        var total = segments?.Sum(s => s.Value) ?? 0;
        if (segments is not null && total > 0)
        {
            var gap = segments.Count > 1 ? 0.035 : 0;
            var start = 0.0;
            for (var i = 0; i < segments.Count; i++)
            {
                var sweep = segments[i].Value / total * Math.PI * 2;
                if (sweep - gap > 0.001)
                {
                    var grow = i == _hover ? 5 : 0;
                    ctx.DrawGeometry(new ImmutableSolidColorBrush(segments[i].Color), null,
                        Ring(c, outer + grow, inner - (i == _hover ? 1 : 0), start + gap / 2, Math.Min(sweep - gap, Math.PI * 2 - 0.0001)));
                }
                start += sweep;
            }
        }

        string title = CenterTitle, value = CenterValue;
        if (segments is not null && _hover >= 0 && _hover < segments.Count)
        {
            title = segments[_hover].Label;
            value = segments[_hover].ValueText;
        }

        var valueText = ChartText.Make(value, Math.Max(12, inner / 3.6), ValueBrush, FontWeight.ExtraBold);
        var titleText = ChartText.Make(title, 11.5, TitleBrush, FontWeight.SemiBold);
        titleText.MaxTextWidth = inner * 1.7;
        titleText.TextAlignment = TextAlignment.Center;
        ChartText.DrawCentered(ctx, valueText, new Point(c.X, c.Y - 8));
        ctx.DrawText(titleText, new Point(c.X - titleText.MaxTextWidth / 2, c.Y + valueText.Height / 2 - 6));
    }

    private static StreamGeometry Ring(Point c, double outer, double inner, double start, double sweep)
    {
        // Кути відраховуються від 12 години за годинниковою стрілкою.
        Point P(double r, double a) => new(c.X + r * Math.Cos(a - Math.PI / 2), c.Y + r * Math.Sin(a - Math.PI / 2));

        var end = start + sweep;
        var large = sweep > Math.PI;
        var g = new StreamGeometry();
        using var ctx = g.Open();
        ctx.BeginFigure(P(outer, start), true);
        ctx.ArcTo(P(outer, end), new Size(outer, outer), 0, large, SweepDirection.Clockwise);
        ctx.LineTo(P(inner, end));
        ctx.ArcTo(P(inner, start), new Size(inner, inner), 0, large, SweepDirection.CounterClockwise);
        ctx.EndFigure(true);
        return g;
    }
}
