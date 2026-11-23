using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void Build_SumsPaidOrders_WithDiscountsAndCategories()
    {
        var orders = new OrderService(_db.Factory);
        var waiter = _db.User("waiter").Id;

        var o1 = orders.OpenOrder(_db.Table(1).Id, waiter, 2);
        orders.AddDish(o1.Id, _db.Dish("Узвар").Id, 2);          // 110
        orders.Pay(o1.Id, PaymentMethod.Cash, 0);

        var o2 = orders.OpenOrder(_db.Table(2).Id, waiter, 2);
        orders.AddDish(o2.Id, _db.Dish("Маргарита").Id);         // 215
        orders.AddDish(o2.Id, _db.Dish("Узвар").Id);             // 55
        orders.Pay(o2.Id, PaymentMethod.Card, 10);               // 270 − 27 = 243

        var open = orders.OpenOrder(_db.Table(3).Id, waiter, 2); // відкрите — не враховується
        orders.AddDish(open.Id, _db.Dish("Стейк Рібай").Id);

        var report = new ReportService(_db.Factory).Build(DateTime.Today, DateTime.Today.AddDays(1));

        Assert.Equal(2, report.OrdersCount);
        Assert.Equal(353m, report.Revenue);
        Assert.Equal(110m, report.CashRevenue);
        Assert.Equal(243m, report.CardRevenue);
        Assert.Equal(27m, report.DiscountsTotal);
        Assert.Equal(176.5m, report.AverageCheck);
        Assert.Equal(353m, report.Categories.Sum(c => c.Revenue));
        Assert.Equal("Узвар", report.TopDishes[0].Name);
        Assert.Equal(3, report.TopDishes[0].Quantity);
    }

    [Fact]
    public void ExportCsv_CreatesFile()
    {
        var service = new ReportService(_db.Factory);
        var report = service.Build(DateTime.Today, DateTime.Today.AddDays(1));
        var dir = Path.Combine(Path.GetTempPath(), "vatra-tests-" + Guid.NewGuid());

        var path = service.ExportCsv(report, dir);

        Assert.True(File.Exists(path));
        Assert.Contains("Виручка", File.ReadAllText(path));
        Directory.Delete(dir, true);
    }

    public void Dispose() => _db.Dispose();
}

public class InventoryServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void Restock_IncreasesQuantity()
    {
        var service = new InventoryService(_db.Factory);
        var salmon = _db.Ingredient("Сьомга");

        service.Restock(salmon.Id, 5);

        Assert.Equal(salmon.Quantity + 5, _db.Ingredient("Сьомга").Quantity);
    }

    [Fact]
    public void Delete_IngredientUsedInRecipe_Throws()
    {
        var service = new InventoryService(_db.Factory);
        Assert.Throws<BusinessException>(() => service.Delete(_db.Ingredient("Картопля").Id));
    }

    [Fact]
    public void Save_DuplicateName_Throws()
    {
        var service = new InventoryService(_db.Factory);
        Assert.Throws<BusinessException>(() =>
            service.Save(new Ingredient { Name = "Картопля", Unit = "кг", Quantity = 1, MinQuantity = 1 }));
    }

    [Fact]
    public void LowAndCriticalFlags_FollowMinimum()
    {
        var i = new Ingredient { Quantity = 4, MinQuantity = 10 };
        Assert.True(i.IsLow);
        Assert.True(i.IsCritical);
        i.Quantity = 7;
        Assert.True(i.IsLow);
        Assert.False(i.IsCritical);
    }

    public void Dispose() => _db.Dispose();
}

public class MenuServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    [Fact]
    public void DeleteDish_UsedInOrders_IsOnlyDisabled()
    {
        var menu = new MenuService(_db.Factory);
        var orders = new OrderService(_db.Factory);
        var dish = _db.Dish("Узвар");
        var order = orders.OpenOrder(_db.Table(1).Id, _db.User("waiter").Id, 1);
        orders.AddDish(order.Id, dish.Id);

        var deleted = menu.DeleteDish(dish.Id);

        Assert.False(deleted);
        Assert.False(_db.Dish("Узвар").IsAvailable);
    }

    [Fact]
    public void SaveDish_ReplacesRecipe()
    {
        var menu = new MenuService(_db.Factory);
        var dish = _db.Dish("Американо");
        var milk = _db.Ingredient("Молоко");

        menu.SaveDish(dish, new[] { (milk.Id, 0.05m) });

        var reloaded = menu.GetDishes().Single(d => d.Id == dish.Id);
        var line = Assert.Single(reloaded.Ingredients);
        Assert.Equal(milk.Id, line.IngredientId);
    }

    [Fact]
    public void DeleteCategory_WithDishes_Throws()
    {
        var menu = new MenuService(_db.Factory);
        var category = menu.GetCategories().First();
        Assert.Throws<BusinessException>(() => menu.DeleteCategory(category.Id));
    }

    public void Dispose() => _db.Dispose();
}

public class FormatTests
{
    [Theory]
    [InlineData(1, "1 гість")]
    [InlineData(2, "2 гості")]
    [InlineData(5, "5 гостей")]
    [InlineData(11, "11 гостей")]
    [InlineData(21, "21 гість")]
    [InlineData(23, "23 гості")]
    [InlineData(112, "112 гостей")]
    public void Plural_UsesUkrainianRules(int n, string expected)
    {
        Assert.Equal(expected, Format.Plural(n, "гість", "гості", "гостей"));
    }

    [Fact]
    public void Receipt_ContainsTotalsAndDiscount()
    {
        var order = new Order
        {
            Id = 42,
            Table = new RestaurantTable { Number = 7 },
            Waiter = new Employee { FullName = "Тест Тестович" },
            DiscountPercent = 10,
            PaymentMethod = PaymentMethod.Card,
            ClosedAt = new DateTime(2026, 1, 1, 20, 0, 0),
            Items = { new OrderItem { Dish = new Dish { Name = "Борщ" }, Price = 100, Quantity = 2 } }
        };

        var text = ReceiptPrinter.Build(order);

        Assert.Contains("Чек № 000042", text);
        Assert.Contains("Знижка 10%", text);
        Assert.Contains("180,00", text);
        Assert.Contains("Картка", text);
    }
}
