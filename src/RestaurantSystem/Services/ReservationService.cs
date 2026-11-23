using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Бронювання столів з перевіркою місткості та перетину за часом.</summary>
public sealed class ReservationService
{
    private readonly DbContextFactory _db;

    public ReservationService(DbContextFactory db)
    {
        _db = db;
    }

    public List<Reservation> GetForDay(DateTime day)
    {
        using var db = _db.Create();
        var from = day.Date;
        var to = from.AddDays(1);
        return db.Reservations.AsNoTracking()
            .Include(r => r.Table)
            .Where(r => r.StartsAt >= from && r.StartsAt < to)
            .OrderBy(r => r.StartsAt)
            .ToList();
    }

    /// <summary>Кількість активних бронювань по днях у діапазоні (для календарної стрічки).</summary>
    public Dictionary<DateTime, int> CountByDay(DateTime from, int days)
    {
        using var db = _db.Create();
        var start = from.Date;
        var end = start.AddDays(days);
        return db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Active && r.StartsAt >= start && r.StartsAt < end)
            .Select(r => r.StartsAt)
            .ToList()
            .GroupBy(d => d.Date)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public Reservation Save(Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(reservation.GuestName))
            throw new BusinessException("Вкажіть ім'я гостя");
        if (string.IsNullOrWhiteSpace(reservation.Phone))
            throw new BusinessException("Вкажіть номер телефону");
        if (reservation.GuestsCount <= 0)
            throw new BusinessException("Кількість гостей має бути більшою за нуль");
        if (reservation.Id == 0 && reservation.StartsAt < DateTime.Now.AddMinutes(-15))
            throw new BusinessException("Неможливо створити бронювання на минулий час");

        using var db = _db.Create();
        var table = db.Tables.Find(reservation.TableId) ?? throw new BusinessException("Оберіть стіл");
        if (reservation.GuestsCount > table.Seats)
            throw new BusinessException($"Стіл №{table.Number} розрахований лише на {table.Seats} гостей");

        var start = reservation.StartsAt;
        var end = start.AddMinutes(reservation.DurationMinutes);
        var conflict = db.Reservations.AsNoTracking()
            .Where(r => r.Id != reservation.Id && r.TableId == reservation.TableId &&
                        r.Status == ReservationStatus.Active &&
                        r.StartsAt < end && r.StartsAt > start.AddHours(-6))
            .AsEnumerable()
            .FirstOrDefault(r => r.EndsAt > start);
        if (conflict is not null)
            throw new BusinessException(
                $"Стіл №{table.Number} вже заброньовано на {conflict.StartsAt:HH:mm}–{conflict.EndsAt:HH:mm} ({conflict.GuestName})");

        var entity = reservation.Id == 0 ? new Reservation() : db.Reservations.Find(reservation.Id)
            ?? throw new BusinessException("Бронювання не знайдено");
        entity.TableId = reservation.TableId;
        entity.GuestName = reservation.GuestName.Trim();
        entity.Phone = reservation.Phone.Trim();
        entity.GuestsCount = reservation.GuestsCount;
        entity.StartsAt = reservation.StartsAt;
        entity.DurationMinutes = reservation.DurationMinutes;
        entity.Note = reservation.Note.Trim();
        if (reservation.Id == 0)
            db.Reservations.Add(entity);
        db.SaveChanges();
        return entity;
    }

    public void SetStatus(int reservationId, ReservationStatus status)
    {
        using var db = _db.Create();
        var entity = db.Reservations.Find(reservationId) ?? throw new BusinessException("Бронювання не знайдено");
        if (entity.Status != ReservationStatus.Active)
            throw new BusinessException("Бронювання вже закрито");
        entity.Status = status;
        db.SaveChanges();
    }
}
