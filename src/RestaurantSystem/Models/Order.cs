namespace RestaurantSystem.Models;

/// <summary>Замовлення (рахунок) за столом.</summary>
public class Order
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public int WaiterId { get; set; }
    public Employee? Waiter { get; set; }

    public int GuestsCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Open;

    /// <summary>Знижка у відсотках (0..100).</summary>
    public decimal DiscountPercent { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal DiscountAmount => Math.Round(Subtotal * DiscountPercent / 100m, 2);
    public decimal Total => Subtotal - DiscountAmount;
}

/// <summary>Позиція замовлення.</summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int DishId { get; set; }
    public Dish? Dish { get; set; }

    public int Quantity { get; set; } = 1;
    /// <summary>Ціна на момент замовлення (меню може змінитися пізніше).</summary>
    public decimal Price { get; set; }
    public string Note { get; set; } = string.Empty;
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Draft;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadyAt { get; set; }

    public decimal LineTotal => Price * Quantity;
}
