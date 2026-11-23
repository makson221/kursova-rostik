using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RestaurantSystem.Controls;

/// <summary>Тонкий індикатор рівня (0..1) із заокругленими краями.</summary>
public class LevelMeter : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(Value));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LevelMeter, IBrush?>(nameof(Fill));

    private static readonly IBrush TrackBrush = new ImmutableSolidColorBrush(Color.Parse("#2C241E"));

    static LevelMeter()
    {
        AffectsRender<LevelMeter>(ValueProperty, FillProperty);
    }

    public LevelMeter()
    {
        Height = 6;
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        var r = Bounds.Height / 2;
        ctx.DrawRectangle(TrackBrush, null, new RoundedRect(new Rect(Bounds.Size), r));
        var w = Bounds.Width * Math.Clamp(Value, 0, 1);
        if (w > 0.5)
            ctx.DrawRectangle(Fill ?? Brushes.Orange, null,
                new RoundedRect(new Rect(0, 0, Math.Max(w, Bounds.Height), Bounds.Height), r));
    }
}
