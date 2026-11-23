using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RestaurantSystem.Infrastructure;

/// <summary>Кольори статусів для використання у ViewModel (дублюють Palette.axaml).</summary>
public static class Tones
{
    public static readonly Color AccentColor = Color.Parse("#F2A65A");
    public static readonly Color EmberColor = Color.Parse("#E8553A");
    public static readonly Color GreenColor = Color.Parse("#6FCF97");
    public static readonly Color BlueColor = Color.Parse("#6FA8F2");
    public static readonly Color RedColor = Color.Parse("#EF6B5E");
    public static readonly Color YellowColor = Color.Parse("#F4D35E");
    public static readonly Color PurpleColor = Color.Parse("#B794F6");
    public static readonly Color MutedColor = Color.Parse("#A89A8D");

    public static readonly IBrush Accent = new ImmutableSolidColorBrush(AccentColor);
    public static readonly IBrush Green = new ImmutableSolidColorBrush(GreenColor);
    public static readonly IBrush Blue = new ImmutableSolidColorBrush(BlueColor);
    public static readonly IBrush Red = new ImmutableSolidColorBrush(RedColor);
    public static readonly IBrush Yellow = new ImmutableSolidColorBrush(YellowColor);
    public static readonly IBrush Purple = new ImmutableSolidColorBrush(PurpleColor);
    public static readonly IBrush Muted = new ImmutableSolidColorBrush(MutedColor);

    public static IBrush Soft(Color color, byte alpha = 0x26) =>
        new ImmutableSolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

    public static IBrush Solid(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));

    public static IBrush Gradient(Color from, Color to) => new LinearGradientBrush
    {
        StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
        EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
        GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) }
    }.ToImmutable();
}
