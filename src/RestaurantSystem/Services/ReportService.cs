using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

public sealed record DailyValue(DateTime Date, decimal Revenue, int Orders);
public sealed record CategoryShare(string Name, string Color, decimal Revenue);
public sealed record DishStat(string Name, string Category, int Quantity, decimal Revenue);
public sealed record WaiterStat(string Name, int Orders, decimal Revenue, decimal AverageCheck);

/// <summary>Агреговані показники за період.</summary>
public sealed class SalesReport
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public decimal Revenue { get; init; }
    public int OrdersCount { get; init; }
    public int GuestsCount { get; init; }
    public decimal AverageCheck => OrdersCount == 0 ? 0 : Math.Round(Revenue / OrdersCount, 2);
    public decimal DiscountsTotal { get; init; }
    public decimal CashRevenue { get; init; }
    public decimal CardRevenue { get; init; }
    public decimal FoodCost { get; init; }
    public IReadOnlyList<DailyValue> Daily { get; init; } = Array.Empty<DailyValue>();
    public IReadOnlyList<CategoryShare> Categories { get; init; } = Array.Empty<CategoryShare>();
    public IReadOnlyList<DishStat> TopDishes { get; init; } = Array.Empty<DishStat>();
    public IReadOnlyList<WaiterStat> Waiters { get; init; } = Array.Empty<WaiterStat>();
    public IReadOnlyList<int> OrdersByHour { get; init; } = Array.Empty<int>();
}

/// <summary>Аналітика продажів та експорт звітів.</summary>
public sealed class ReportService
{
    private readonly DbContextFactory _db;

    public ReportService(DbContextFactory db)
    {
        _db = db;
    }

    /// <param name="from">Початок періоду (включно).</param>
    /// <param name="to">Кінець періоду (не включно).</param>
    public SalesReport Build(DateTime from, DateTime to)
    {
        using var db = _db.Create();

        // SQLite не вміє агрегувати decimal на стороні БД, тому рахуємо в пам'яті.
        // Identity resolution потрібна, щоб однакові страви/категорії були одним об'єктом при групуванні.
        var orders = db.Orders.AsNoTrackingWithIdentityResolution()
            .Include(o => o.Waiter)
            .Include(o => o.Items).ThenInclude(i => i.Dish).ThenInclude(d => d!.Category)
            .Include(o => o.Items).ThenInclude(i => i.Dish).ThenInclude(d => d!.Ingredients).ThenInclude(r => r.Ingredient)
            .Where(o => o.Status == OrderStatus.Paid && o.ClosedAt >= from && o.ClosedAt < to)
            .AsSplitQuery()
            .ToList();

        var items = orders.SelectMany(o => o.Items.Select(i => (Order: o, Item: i))).ToList();

        // Частка кожної позиції з урахуванням знижки на чек.
        decimal Net((Order Order, OrderItem Item) x) => x.Item.LineTotal * (1 - x.Order.DiscountPercent / 100m);

        var days = (int)Math.Ceiling((to.Date - from.Date).TotalDays);
        var daily = Enumerable.Range(0, Math.Max(days, 1))
            .Select(d => from.Date.AddDays(d))
            .Select(date =>
            {
                var dayOrders = orders.Where(o => o.ClosedAt!.Value.Date == date).ToList();
                return new DailyValue(date, dayOrders.Sum(o => o.Total), dayOrders.Count);
            })
            .ToList();

        var categories = items
            .GroupBy(x => x.Item.Dish!.Category!)
            .Select(g => new CategoryShare(g.Key.Name, g.Key.Color, Math.Round(g.Sum(Net), 2)))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var topDishes = items
            .GroupBy(x => x.Item.Dish!)
            .Select(g => new DishStat(g.Key.Name, g.Key.Category!.Name, g.Sum(x => x.Item.Quantity), Math.Round(g.Sum(Net), 2)))
            .OrderByDescending(d => d.Quantity).ThenByDescending(d => d.Revenue)
            .ToList();

        var waiters = orders
            .GroupBy(o => o.Waiter!.FullName)
            .Select(g => new WaiterStat(g.Key, g.Count(), g.Sum(o => o.Total), Math.Round(g.Average(o => o.Total), 2)))
            .OrderByDescending(w => w.Revenue)
            .ToList();

        var byHour = new int[24];
        foreach (var o in orders)
            byHour[o.CreatedAt.Hour]++;

        var foodCost = items.Sum(x =>
            x.Item.Dish!.Ingredients.Sum(r => r.Amount * r.Ingredient!.CostPerUnit) * x.Item.Quantity);

        return new SalesReport
        {
            From = from,
            To = to,
            Revenue = orders.Sum(o => o.Total),
            OrdersCount = orders.Count,
            GuestsCount = orders.Sum(o => o.GuestsCount),
            DiscountsTotal = orders.Sum(o => o.DiscountAmount),
            CashRevenue = orders.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.Total),
            CardRevenue = orders.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.Total),
            FoodCost = Math.Round(foodCost, 2),
            Daily = daily,
            Categories = categories,
            TopDishes = topDishes,
            Waiters = waiters,
            OrdersByHour = byHour
        };
    }

    /// <summary>Експортує звіт у CSV (роздільник «;», UTF-8 з BOM — коректно відкривається в Excel).</summary>
    public string ExportCsv(SalesReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,
            $"Звіт_{report.From:yyyy-MM-dd}_{report.To.AddDays(-1):yyyy-MM-dd}.csv");

        var c = CultureInfo.GetCultureInfo("uk-UA");
        var sb = new StringBuilder();
        sb.AppendLine("Звіт про продажі;Ресторан «Ватра»");
        sb.AppendLine($"Період;{report.From:dd.MM.yyyy} – {report.To.AddDays(-1):dd.MM.yyyy}");
        sb.AppendLine();
        sb.AppendLine("Показник;Значення");
        sb.AppendLine($"Виручка, грн;{report.Revenue.ToString("F2", c)}");
        sb.AppendLine($"Кількість чеків;{report.OrdersCount}");
        sb.AppendLine($"Середній чек, грн;{report.AverageCheck.ToString("F2", c)}");
        sb.AppendLine($"Гостей;{report.GuestsCount}");
        sb.AppendLine($"Знижки, грн;{report.DiscountsTotal.ToString("F2", c)}");
        sb.AppendLine($"Готівка, грн;{report.CashRevenue.ToString("F2", c)}");
        sb.AppendLine($"Картка, грн;{report.CardRevenue.ToString("F2", c)}");
        sb.AppendLine($"Собівартість продуктів, грн;{report.FoodCost.ToString("F2", c)}");
        sb.AppendLine();
        sb.AppendLine("Дата;Чеків;Виручка, грн");
        foreach (var d in report.Daily)
            sb.AppendLine($"{d.Date:dd.MM.yyyy};{d.Orders};{d.Revenue.ToString("F2", c)}");
        sb.AppendLine();
        sb.AppendLine("Категорія;Виручка, грн");
        foreach (var cat in report.Categories)
            sb.AppendLine($"{Csv(cat.Name)};{cat.Revenue.ToString("F2", c)}");
        sb.AppendLine();
        sb.AppendLine("Страва;Категорія;Продано, шт;Виручка, грн");
        foreach (var dish in report.TopDishes)
            sb.AppendLine($"{Csv(dish.Name)};{Csv(dish.Category)};{dish.Quantity};{dish.Revenue.ToString("F2", c)}");
        sb.AppendLine();
        sb.AppendLine("Офіціант;Чеків;Виручка, грн;Середній чек, грн");
        foreach (var w in report.Waiters)
            sb.AppendLine($"{Csv(w.Name)};{w.Orders};{w.Revenue.ToString("F2", c)};{w.AverageCheck.ToString("F2", c)}");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        return path;
    }

    private static string Csv(string value) =>
        value.Contains(';') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
