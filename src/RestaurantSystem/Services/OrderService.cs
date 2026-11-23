using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>
/// Робота із замовленнями: відкриття рахунку, додавання страв, передача на кухню,
/// черга кухні, розрахунок гостя.
/// </summary>
public sealed class OrderService
{
    private readonly DbContextFactory _db;

    public OrderService(DbContextFactory db)
    {
        _db = db;
    }

    private static IQueryable<Order> WithDetails(IQueryable<Order> q) => q
        .Include(o => o.Table)
        .Include(o => o.Waiter)
        .Include(o => o.Items).ThenInclude(i => i.Dish).ThenInclude(d => d!.Category);

    public Order? GetOpenOrderForTable(int tableId)
    {
        using var db = _db.Create();
        return WithDetails(db.Orders.AsNoTracking())
            .FirstOrDefault(o => o.TableId == tableId && o.Status == OrderStatus.Open);
    }

    public Order GetOrder(int orderId)
    {
        using var db = _db.Create();
        return WithDetails(db.Orders.AsNoTracking()).FirstOrDefault(o => o.Id == orderId)
               ?? throw new BusinessException("Замовлення не знайдено");
    }

    public Order OpenOrder(int tableId, int waiterId, int guests)
    {
        using var db = _db.Create();
        var table = db.Tables.Find(tableId) ?? throw new BusinessException("Стіл не знайдено");
        if (db.Orders.Any(o => o.TableId == tableId && o.Status == OrderStatus.Open))
            throw new BusinessException($"Стіл №{table.Number} вже має відкрите замовлення");

        var order = new Order
        {
            TableId = tableId,
            WaiterId = waiterId,
            GuestsCount = Math.Clamp(guests, 1, Math.Max(table.Seats * 2, 1)),
            CreatedAt = DateTime.Now
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    public void SetGuests(int orderId, int guests)
    {
        using var db = _db.Create();
        var order = LoadOpen(db, orderId);
        order.GuestsCount = Math.Max(1, guests);
        db.SaveChanges();
    }

    public void AddDish(int orderId, int dishId, int quantity = 1, string note = "")
    {
        if (quantity <= 0)
            throw new BusinessException("Кількість має бути більшою за нуль");

        using var db = _db.Create();
        var order = LoadOpen(db, orderId);
        var dish = db.Dishes.Find(dishId) ?? throw new BusinessException("Страву не знайдено");
        if (!dish.IsAvailable)
            throw new BusinessException($"«{dish.Name}» зараз недоступна");

        note = note.Trim();
        var existing = order.Items.FirstOrDefault(i =>
            i.DishId == dishId && i.Status == OrderItemStatus.Draft && i.Note == note);
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            order.Items.Add(new OrderItem
            {
                DishId = dishId,
                Price = dish.Price,
                Quantity = quantity,
                Note = note,
                Status = OrderItemStatus.Draft
            });
        }
        db.SaveChanges();
    }

    public void ChangeQuantity(int itemId, int delta)
    {
        using var db = _db.Create();
        var item = LoadDraftItem(db, itemId);
        item.Quantity += delta;
        if (item.Quantity <= 0)
            db.OrderItems.Remove(item);
        db.SaveChanges();
    }

    public void RemoveItem(int itemId)
    {
        using var db = _db.Create();
        var item = LoadDraftItem(db, itemId);
        db.OrderItems.Remove(item);
        db.SaveChanges();
    }

    public void SetNote(int itemId, string note)
    {
        using var db = _db.Create();
        var item = LoadDraftItem(db, itemId);
        item.Note = note.Trim();
        db.SaveChanges();
    }

    /// <summary>
    /// Передає всі чернеткові позиції на кухню та списує інгредієнти зі складу.
    /// Повертає перелік інгредієнтів, що опустилися нижче мінімального залишку.
    /// </summary>
    public (int SentCount, List<string> LowStock) SendToKitchen(int orderId)
    {
        using var db = _db.Create();
        var order = LoadOpen(db, orderId);
        var drafts = order.Items.Where(i => i.Status == OrderItemStatus.Draft).ToList();
        if (drafts.Count == 0)
            throw new BusinessException("Немає нових позицій для передачі на кухню");

        var now = DateTime.Now;
        foreach (var item in drafts)
        {
            item.Status = OrderItemStatus.New;
            item.SentAt = now;
        }
        var low = InventoryService.WriteOff(db, drafts);
        db.SaveChanges();
        return (drafts.Sum(d => d.Quantity), low);
    }

    /// <summary>Закриває рахунок: фіксує оплату, знижку та звільняє стіл.</summary>
    public Order Pay(int orderId, PaymentMethod method, decimal discountPercent)
    {
        if (discountPercent is < 0 or > 100)
            throw new BusinessException("Знижка має бути в межах 0–100 %");

        using var db = _db.Create();
        var order = LoadOpen(db, orderId);
        if (order.Items.Count == 0)
            throw new BusinessException("Неможливо розрахувати порожнє замовлення");

        var now = DateTime.Now;
        var drafts = order.Items.Where(i => i.Status == OrderItemStatus.Draft).ToList();
        InventoryService.WriteOff(db, drafts);
        foreach (var item in order.Items)
        {
            item.SentAt ??= now;
            item.ReadyAt ??= now;
            item.Status = OrderItemStatus.Served;
        }

        order.DiscountPercent = discountPercent;
        order.PaymentMethod = method;
        order.Status = OrderStatus.Paid;
        order.ClosedAt = now;
        db.SaveChanges();

        return GetOrder(orderId);
    }

    public void Cancel(int orderId)
    {
        using var db = _db.Create();
        var order = LoadOpen(db, orderId);
        if (order.Items.Any(i => i.Status != OrderItemStatus.Draft))
            throw new BusinessException("Замовлення вже передано на кухню — його можна лише розрахувати");

        order.Status = OrderStatus.Cancelled;
        order.ClosedAt = DateTime.Now;
        db.OrderItems.RemoveRange(order.Items);
        db.SaveChanges();
    }

    // ---------------- Кухня ----------------

    public List<OrderItem> GetKitchenQueue()
    {
        using var db = _db.Create();
        return db.OrderItems.AsNoTracking()
            .Include(i => i.Dish)
            .Include(i => i.Order).ThenInclude(o => o!.Table)
            .Include(i => i.Order).ThenInclude(o => o!.Waiter)
            .Where(i => i.Order!.Status == OrderStatus.Open &&
                        (i.Status == OrderItemStatus.New ||
                         i.Status == OrderItemStatus.Cooking ||
                         i.Status == OrderItemStatus.Ready))
            .OrderBy(i => i.SentAt)
            .ToList();
    }

    /// <summary>Переводить позицію на наступний етап: Нова → Готується → Готово → Видано.</summary>
    public OrderItemStatus AdvanceItem(int itemId)
    {
        using var db = _db.Create();
        var item = db.OrderItems.Include(i => i.Order).FirstOrDefault(i => i.Id == itemId)
                   ?? throw new BusinessException("Позицію не знайдено");
        if (item.Order!.Status != OrderStatus.Open)
            throw new BusinessException("Замовлення вже закрито");

        item.Status = item.Status switch
        {
            OrderItemStatus.New => OrderItemStatus.Cooking,
            OrderItemStatus.Cooking => OrderItemStatus.Ready,
            OrderItemStatus.Ready => OrderItemStatus.Served,
            _ => throw new BusinessException("Позицію не можна перевести на наступний етап")
        };
        if (item.Status == OrderItemStatus.Ready)
            item.ReadyAt = DateTime.Now;
        db.SaveChanges();
        return item.Status;
    }

    // ---------------- helpers ----------------

    private static Order LoadOpen(RestaurantDbContext db, int orderId)
    {
        var order = db.Orders.Include(o => o.Items).FirstOrDefault(o => o.Id == orderId)
                    ?? throw new BusinessException("Замовлення не знайдено");
        if (order.Status != OrderStatus.Open)
            throw new BusinessException("Замовлення вже закрито");
        return order;
    }

    private static OrderItem LoadDraftItem(RestaurantDbContext db, int itemId)
    {
        var item = db.OrderItems.Include(i => i.Order).FirstOrDefault(i => i.Id == itemId)
                   ?? throw new BusinessException("Позицію не знайдено");
        if (item.Order!.Status != OrderStatus.Open)
            throw new BusinessException("Замовлення вже закрито");
        if (item.Status != OrderItemStatus.Draft)
            throw new BusinessException("Позиція вже на кухні — змінити її неможливо");
        return item;
    }
}
