namespace RestaurantSystem.Models;

/// <summary>Роль працівника в системі — визначає доступні розділи.</summary>
public enum UserRole
{
    Administrator,
    Waiter,
    Cook
}

/// <summary>Стан замовлення (чеку) за столом.</summary>
public enum OrderStatus
{
    Open,
    Paid,
    Cancelled
}

/// <summary>Життєвий цикл окремої позиції замовлення.</summary>
public enum OrderItemStatus
{
    /// <summary>Додано офіціантом, ще не відправлено на кухню.</summary>
    Draft,
    /// <summary>Відправлено на кухню, очікує приготування.</summary>
    New,
    /// <summary>Готується кухарем.</summary>
    Cooking,
    /// <summary>Готово, очікує видачі.</summary>
    Ready,
    /// <summary>Видано гостю.</summary>
    Served
}

public enum PaymentMethod
{
    Cash,
    Card
}

public enum ReservationStatus
{
    Active,
    Arrived,
    Cancelled
}

public enum TableShape
{
    Round,
    Square,
    Long
}

public static class EnumText
{
    public static string ToText(this UserRole role) => role switch
    {
        UserRole.Administrator => "Адміністратор",
        UserRole.Waiter => "Офіціант",
        UserRole.Cook => "Кухар",
        _ => role.ToString()
    };

    public static string ToText(this OrderItemStatus status) => status switch
    {
        OrderItemStatus.Draft => "Чернетка",
        OrderItemStatus.New => "На кухні",
        OrderItemStatus.Cooking => "Готується",
        OrderItemStatus.Ready => "Готово",
        OrderItemStatus.Served => "Видано",
        _ => status.ToString()
    };

    public static string ToText(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Готівка",
        PaymentMethod.Card => "Картка",
        _ => method.ToString()
    };

    public static string ToText(this ReservationStatus status) => status switch
    {
        ReservationStatus.Active => "Очікується",
        ReservationStatus.Arrived => "Гості прийшли",
        ReservationStatus.Cancelled => "Скасовано",
        _ => status.ToString()
    };
}
