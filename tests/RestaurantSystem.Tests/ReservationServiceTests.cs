using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.Tests;

public class ReservationServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ReservationService _service;
    private readonly DateTime _tomorrow19 = DateTime.Today.AddDays(1).AddHours(19);

    public ReservationServiceTests()
    {
        _service = new ReservationService(_db.Factory);
    }

    private Reservation Make(int table, DateTime start, int guests = 2) => new()
    {
        TableId = _db.Table(table).Id,
        GuestName = "Тестовий гість",
        Phone = "+380 00 000 00 00",
        GuestsCount = guests,
        StartsAt = start,
        DurationMinutes = 120
    };

    [Fact]
    public void Save_ValidReservation_Persists()
    {
        var saved = _service.Save(Make(5, _tomorrow19));
        Assert.True(saved.Id > 0);
        Assert.Single(_service.GetForDay(_tomorrow19));
    }

    [Fact]
    public void Save_OverlappingReservation_Throws()
    {
        _service.Save(Make(5, _tomorrow19));
        var ex = Assert.Throws<BusinessException>(() => _service.Save(Make(5, _tomorrow19.AddHours(1))));
        Assert.Contains("вже заброньовано", ex.Message);
    }

    [Fact]
    public void Save_AdjacentReservation_IsAllowed()
    {
        _service.Save(Make(5, _tomorrow19));
        _service.Save(Make(5, _tomorrow19.AddHours(2)));
        Assert.Equal(2, _service.GetForDay(_tomorrow19).Count);
    }

    [Fact]
    public void Save_CancelledReservation_DoesNotBlockTable()
    {
        var first = _service.Save(Make(5, _tomorrow19));
        _service.SetStatus(first.Id, ReservationStatus.Cancelled);
        _service.Save(Make(5, _tomorrow19));
    }

    [Fact]
    public void Save_TooManyGuests_Throws()
    {
        // Стіл №1 — на 2 місця
        Assert.Throws<BusinessException>(() => _service.Save(Make(1, _tomorrow19, guests: 5)));
    }

    [Fact]
    public void Save_InThePast_Throws()
    {
        Assert.Throws<BusinessException>(() => _service.Save(Make(5, DateTime.Now.AddDays(-1))));
    }

    public void Dispose() => _db.Dispose();
}
