using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.Tests;

public class OrderServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly OrderService _orders;
    private readonly int _waiterId;

    public OrderServiceTests()
    {
        _orders = new OrderService(_db.Factory);
        _waiterId = _db.User("waiter").Id;
    }

    private Order NewOrder(int table = 1) => _orders.OpenOrder(_db.Table(table).Id, _waiterId, 2);

    [Fact]
    public void OpenOrder_TwiceForSameTable_Throws()
    {
        NewOrder();
        Assert.Throws<BusinessException>(() => NewOrder());
    }

    [Fact]
    public void AddDish_SameDishTwice_MergesIntoOneDraftLine()
    {
        var order = NewOrder();
        var dish = _db.Dish("Узвар");

        _orders.AddDish(order.Id, dish.Id);
        _orders.AddDish(order.Id, dish.Id, 2);

        var loaded = _orders.GetOrder(order.Id);
        var line = Assert.Single(loaded.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(OrderItemStatus.Draft, line.Status);
        Assert.Equal(dish.Price * 3, loaded.Subtotal);
    }

    [Fact]
    public void AddDish_WithDifferentNote_CreatesSeparateLine()
    {
        var order = NewOrder();
        var dish = _db.Dish("Цезар з куркою");

        _orders.AddDish(order.Id, dish.Id);
        _orders.AddDish(order.Id, dish.Id, 1, "соус окремо");

        Assert.Equal(2, _orders.GetOrder(order.Id).Items.Count);
    }

    [Fact]
    public void AddDish_Unavailable_Throws()
    {
        var order = NewOrder();
        var dish = _db.Dish("Медовик");
        new MenuService(_db.Factory).SetAvailability(dish.Id, false);

        Assert.Throws<BusinessException>(() => _orders.AddDish(order.Id, dish.Id));
    }

    [Fact]
    public void ChangeQuantity_ToZero_RemovesLine()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Американо").Id);
        var item = _orders.GetOrder(order.Id).Items.Single();

        _orders.ChangeQuantity(item.Id, -1);

        Assert.Empty(_orders.GetOrder(order.Id).Items);
    }

    [Fact]
    public void SendToKitchen_WritesOffIngredientsByRecipe()
    {
        var order = NewOrder();
        var dish = _db.Dish("Капучино"); // 0.018 кг кави + 0.15 л молока
        var coffeeBefore = _db.Ingredient("Кава в зернах").Quantity;
        var milkBefore = _db.Ingredient("Молоко").Quantity;

        _orders.AddDish(order.Id, dish.Id, 2);
        var (sent, _) = _orders.SendToKitchen(order.Id);

        Assert.Equal(2, sent);
        Assert.Equal(coffeeBefore - 0.036m, _db.Ingredient("Кава в зернах").Quantity);
        Assert.Equal(milkBefore - 0.3m, _db.Ingredient("Молоко").Quantity);
        Assert.All(_orders.GetOrder(order.Id).Items, i => Assert.Equal(OrderItemStatus.New, i.Status));
    }

    [Fact]
    public void SendToKitchen_ReportsIngredientsThatBecameLow()
    {
        var inventory = new InventoryService(_db.Factory);
        var milk = _db.Ingredient("Молоко");
        milk.Quantity = milk.MinQuantity + 0.1m;
        inventory.Save(milk);

        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Капучино").Id);
        var (_, low) = _orders.SendToKitchen(order.Id);

        Assert.Contains("Молоко", low);
    }

    [Fact]
    public void SendToKitchen_WithoutDrafts_Throws()
    {
        var order = NewOrder();
        Assert.Throws<BusinessException>(() => _orders.SendToKitchen(order.Id));
    }

    [Fact]
    public void SentItem_CannotBeChanged()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id);
        _orders.SendToKitchen(order.Id);
        var item = _orders.GetOrder(order.Id).Items.Single();

        Assert.Throws<BusinessException>(() => _orders.ChangeQuantity(item.Id, 1));
        Assert.Throws<BusinessException>(() => _orders.RemoveItem(item.Id));
    }

    [Fact]
    public void AdvanceItem_GoesThroughKitchenLifecycle()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Деруни зі сметаною").Id);
        _orders.SendToKitchen(order.Id);
        var id = _orders.GetOrder(order.Id).Items.Single().Id;

        Assert.Equal(OrderItemStatus.Cooking, _orders.AdvanceItem(id));
        Assert.Equal(OrderItemStatus.Ready, _orders.AdvanceItem(id));
        Assert.Equal(OrderItemStatus.Served, _orders.AdvanceItem(id));
        Assert.Throws<BusinessException>(() => _orders.AdvanceItem(id));
    }

    [Fact]
    public void KitchenQueue_ContainsOnlySentNotServedItems()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id);
        _orders.SendToKitchen(order.Id);
        _orders.AddDish(order.Id, _db.Dish("Медовик").Id); // чернетка

        var queue = _orders.GetKitchenQueue();

        var item = Assert.Single(queue);
        Assert.Equal("Узвар", item.Dish!.Name);
    }

    [Fact]
    public void Pay_AppliesDiscount_AndClosesOrder()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Стейк Рібай").Id);   // 690
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id, 2);      // 2 × 55
        _orders.SendToKitchen(order.Id);

        var paid = _orders.Pay(order.Id, PaymentMethod.Card, 10);

        Assert.Equal(OrderStatus.Paid, paid.Status);
        Assert.Equal(800m, paid.Subtotal);
        Assert.Equal(80m, paid.DiscountAmount);
        Assert.Equal(720m, paid.Total);
        Assert.NotNull(paid.ClosedAt);
        Assert.All(paid.Items, i => Assert.Equal(OrderItemStatus.Served, i.Status));
        Assert.Null(_orders.GetOpenOrderForTable(_db.Table(1).Id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Pay_WithInvalidDiscount_Throws(decimal discount)
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id);
        Assert.Throws<BusinessException>(() => _orders.Pay(order.Id, PaymentMethod.Cash, discount));
    }

    [Fact]
    public void Pay_EmptyOrder_Throws()
    {
        var order = NewOrder();
        Assert.Throws<BusinessException>(() => _orders.Pay(order.Id, PaymentMethod.Cash, 0));
    }

    [Fact]
    public void Cancel_AfterSendingToKitchen_Throws()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id);
        _orders.SendToKitchen(order.Id);

        Assert.Throws<BusinessException>(() => _orders.Cancel(order.Id));
    }

    [Fact]
    public void Cancel_DraftOrder_FreesTable()
    {
        var order = NewOrder();
        _orders.AddDish(order.Id, _db.Dish("Узвар").Id);

        _orders.Cancel(order.Id);

        Assert.Null(_orders.GetOpenOrderForTable(_db.Table(1).Id));
        NewOrder(); // стіл знову вільний
    }

    public void Dispose() => _db.Dispose();
}
