namespace RestaurantSystem.Models;

/// <summary>Стіл у залі ресторану.</summary>
public class RestaurantTable
{
    public int Id { get; set; }
    public int Number { get; set; }
    public int Seats { get; set; }
    public string Zone { get; set; } = "Основна зала";
    public TableShape Shape { get; set; } = TableShape.Square;
    /// <summary>Координати на плані зали (умовні одиниці 0..940 × 0..720).</summary>
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>Бронювання столу.</summary>
public class Reservation
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int GuestsCount { get; set; } = 2;
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 120;
    public string Note { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}
