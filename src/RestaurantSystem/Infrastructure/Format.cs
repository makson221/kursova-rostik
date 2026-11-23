using System.Globalization;

namespace RestaurantSystem.Infrastructure;

/// <summary>Форматування значень для українського інтерфейсу.</summary>
public static class Format
{
    public static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public static string Money(decimal value) => value.ToString("N0", Uk) + " ₴";
    public static string MoneyExact(decimal value) => value.ToString("N2", Uk) + " ₴";
    public static string Number(decimal value) => value.ToString("0.###", Uk);

    public static string Minutes(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "щойно";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} хв";
        return $"{(int)span.TotalHours} год {span.Minutes:D2} хв";
    }

    public static string DayName(DateTime date) =>
        Uk.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek);

    public static string LongDate(DateTime date)
    {
        var s = date.ToString("dddd, d MMMM", Uk);
        return char.ToUpper(s[0]) + s[1..];
    }

    /// <summary>Узгодження іменника з числівником: 1 гість, 2 гості, 5 гостей.</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        var n10 = n % 10;
        var n100 = n % 100;
        var word = n10 == 1 && n100 != 11 ? one
            : n10 is >= 2 and <= 4 && (n100 < 12 || n100 > 14) ? few
            : many;
        return $"{n} {word}";
    }

    public static string Percent(decimal value) => value.ToString("0.#", Uk) + " %";
}
