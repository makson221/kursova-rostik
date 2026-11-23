using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace RestaurantSystem.Controls;

internal static class ChartText
{
    private static readonly FontFamily Ui = new(App.UiFontFamily);

    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public static FormattedText Make(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, Uk, FlowDirection.LeftToRight,
            new Typeface(Ui, FontStyle.Normal, weight), size, brush);

    public static void DrawCentered(DrawingContext ctx, FormattedText text, Point center) =>
        ctx.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));

    /// <summary>Компактний запис великих чисел: 12 500 → «12,5 тис».</summary>
    public static string Compact(double value)
    {
        if (value >= 1_000_000) return (value / 1_000_000).ToString("0.#", Uk) + " млн";
        if (value >= 1_000) return (value / 1_000).ToString("0.#", Uk) + " тис";
        return value.ToString("0.#", Uk);
    }

    /// <summary>
    /// «Красива» шкала: верхній край 1, 2, 3, 4, 5, 6, 8 або 10 × 10^n
    /// та кількість поділок, щоб крок був круглим числом.
    /// </summary>
    public static (double Max, int Lines) NiceScale(double max)
    {
        if (max <= 0) return (4, 4);
        var exp = Math.Pow(10, Math.Floor(Math.Log10(max)));
        foreach (var (m, lines) in new[] { (1.0, 5), (2.0, 4), (3.0, 3), (4.0, 4), (5.0, 5), (6.0, 3), (8.0, 4), (10.0, 5) })
            if (m * exp >= max) return (m * exp, lines);
        return (10 * exp, 5);
    }
}
