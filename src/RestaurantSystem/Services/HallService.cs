using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Поточний стан столу: відкрите замовлення та найближче бронювання.</summary>
public sealed record TableState(RestaurantTable Table, Order? OpenOrder, Reservation? UpcomingReservation)
{
    public bool IsOccupied => OpenOrder is not null;
    public bool IsReserved => OpenOrder is null && UpcomingReservation is not null;
}

public sealed class HallService
{
    /// <summary>За скільки хвилин до початку бронювання стіл вважається заброньованим.</summary>
    public const int ReservationHoldMinutes = 180;

    private readonly DbContextFactory _db;

    public HallService(DbContextFactory db)
    {
        _db = db;
    }

    public List<RestaurantTable> GetTables()
    {
        using var db = _db.Create();
        return db.Tables.AsNoTracking().OrderBy(t => t.Number).ToList();
    }

    public List<TableState> GetHallState(DateTime now)
    {
        using var db = _db.Create();
        var tables = db.Tables.AsNoTracking().OrderBy(t => t.Number).ToList();
        var orders = db.Orders.AsNoTracking()
            .Include(o => o.Waiter)
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Open)
            .ToList();

        var from = now.AddMinutes(-30);
        var to = now.AddMinutes(ReservationHoldMinutes);
        var reservations = db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.StartsAt >= from && r.StartsAt <= to)
            .OrderBy(r => r.StartsAt)
            .ToList();

        return tables.Select(t => new TableState(
                t,
                orders.FirstOrDefault(o => o.TableId == t.Id),
                reservations.FirstOrDefault(r => r.TableId == t.Id)))
            .ToList();
    }
}
