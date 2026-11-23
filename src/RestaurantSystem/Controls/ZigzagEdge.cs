using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RestaurantSystem.Controls;

/// <summary>«Відірваний» зубчастий край паперового чеку.</summary>
public class ZigzagEdge : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ZigzagEdge, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<bool> PointDownProperty =
        AvaloniaProperty.Register<ZigzagEdge, bool>(nameof(PointDown), true);

    public static readonly StyledProperty<double> ToothProperty =
        AvaloniaProperty.Register<ZigzagEdge, double>(nameof(Tooth), 14);

    static ZigzagEdge()
    {
        AffectsRender<ZigzagEdge>(FillProperty, PointDownProperty, ToothProperty);
    }

    public ZigzagEdge()
    {
        Height = 8;
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>true — зубці донизу (низ чеку), false — догори (верх).</summary>
    public bool PointDown
    {
        get => GetValue(PointDownProperty);
        set => SetValue(PointDownProperty, value);
    }

    public double Tooth
    {
        get => GetValue(ToothProperty);
        set => SetValue(ToothProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0 || Fill is null) return;

        var count = Math.Max(1, (int)Math.Round(w / Tooth));
        var step = w / count;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            var baseY = PointDown ? 0 : h;
            var tipY = PointDown ? h : 0;
            c.BeginFigure(new Point(0, baseY), true);
            for (var i = 0; i < count; i++)
            {
                c.LineTo(new Point(step * i + step / 2, tipY));
                c.LineTo(new Point(step * (i + 1), baseY));
            }
            c.EndFigure(true);
        }
        ctx.DrawGeometry(Fill, null, g);
    }
}
