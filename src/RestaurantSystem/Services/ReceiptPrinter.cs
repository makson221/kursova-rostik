using System.Globalization;
using System.Text;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Формує текст фіскального чеку (моноширинний, 40 символів).</summary>
public static class ReceiptPrinter
{
    private const int Width = 40;
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public static string Build(Order order)
    {
        var sb = new StringBuilder();
        Center(sb, "РЕСТОРАН «ВАТРА»");
        Center(sb, "м. Львів, вул. Вогняна, 7");
        Center(sb, "тел. +380 32 000 00 77");
        Line(sb, '=');
        sb.AppendLine($"Чек № {order.Id:D6}");
        sb.AppendLine($"Стіл № {order.Table?.Number}   Гостей: {order.GuestsCount}");
        sb.AppendLine($"Офіціант: {order.Waiter?.FullName}");
        sb.AppendLine($"Дата: {(order.ClosedAt ?? DateTime.Now):dd.MM.yyyy HH:mm}");
        Line(sb, '-');

        foreach (var item in order.Items)
        {
            var name = item.Dish?.Name ?? "Страва";
            foreach (var chunk in Wrap(name, Width))
                sb.AppendLine(chunk);
            Pair(sb, $"  {item.Quantity} x {Money(item.Price)}", Money(item.LineTotal));
        }

        Line(sb, '-');
        Pair(sb, "Сума:", Money(order.Subtotal));
        if (order.DiscountPercent > 0)
            Pair(sb, $"Знижка {order.DiscountPercent:0.#}%:", "-" + Money(order.DiscountAmount));
        Line(sb, '=');
        Pair(sb, "ДО СПЛАТИ:", Money(order.Total) + " грн");
        Pair(sb, "Оплата:", order.PaymentMethod?.ToText() ?? "—");
        Line(sb, '=');
        Center(sb, "Дякуємо, що завітали!");
        Center(sb, "Чекаємо на вас знову!");
        return sb.ToString();
    }

    private static string Money(decimal value) => value.ToString("N2", Uk);

    private static void Line(StringBuilder sb, char c) => sb.AppendLine(new string(c, Width));

    private static void Center(StringBuilder sb, string text)
    {
        var pad = Math.Max(0, (Width - text.Length) / 2);
        sb.AppendLine(new string(' ', pad) + text);
    }

    private static void Pair(StringBuilder sb, string left, string right)
    {
        var spaces = Math.Max(1, Width - left.Length - right.Length);
        sb.AppendLine(left + new string(' ', spaces) + right);
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (line.Length + word.Length + 1 > width && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            yield return line.ToString();
    }
}
