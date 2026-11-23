using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Controls;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed record KpiCard(
    string Title, string Value, string Caption, MaterialIconKind Icon,
    IBrush Tone, IBrush ToneSoft, bool? Trend)
{
    public IBrush CaptionTone => Trend switch
    {
        true => Tones.Green,
        false => Tones.Red,
        _ => Tones.Muted
    };
}

public sealed record RankedRow(int Rank, string Title, string Subtitle, string Value, double Ratio);

public sealed record AlertRow(string Title, string Value, IBrush Tone, IBrush ToneSoft, MaterialIconKind Icon);

/// <summary>Головна панель адміністратора: ключові показники зміни.</summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly ReportService _reports;
    private readonly HallService _hall;
    private readonly OrderService _orders;
    private readonly InventoryService _inventory;
    private readonly ReservationService _reservations;
    private readonly AuthService _auth;
    private readonly NavigationService _navigation;

    public DashboardViewModel(
        ToastService toasts, ReportService reports, HallService hall, OrderService orders,
        InventoryService inventory, ReservationService reservations, AuthService auth, NavigationService navigation)
        : base(toasts)
    {
        _reports = reports;
        _hall = hall;
        _orders = orders;
        _inventory = inventory;
        _reservations = reservations;
        _auth = auth;
        _navigation = navigation;
    }

    public override string Title => "Огляд";

    public IReadOnlyList<KpiCard> Kpis { get; private set; } = Array.Empty<KpiCard>();
    public IReadOnlyList<ChartPoint> RevenueChart { get; private set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> HourlyChart { get; private set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<DonutSegment> CategorySegments { get; private set; } = Array.Empty<DonutSegment>();
    public IReadOnlyList<RankedRow> TopDishes { get; private set; } = Array.Empty<RankedRow>();
    public IReadOnlyList<AlertRow> LowStock { get; private set; } = Array.Empty<AlertRow>();
    public IReadOnlyList<RankedRow> UpcomingReservations { get; private set; } = Array.Empty<RankedRow>();

    public string Revenue14Text { get; private set; } = string.Empty;
    public string CategoryTotalText { get; private set; } = string.Empty;
    public int KitchenNew { get; private set; }
    public int KitchenCooking { get; private set; }
    public int KitchenReady { get; private set; }
    public bool HasLowStock => LowStock.Count > 0;
    public bool HasReservations => UpcomingReservations.Count > 0;

    public override void OnActivated() => Load();

    [RelayCommand]
    private void Load()
    {
        var now = DateTime.Now;
        var today = _reports.Build(now.Date, now.Date.AddDays(1));
        var yesterday = _reports.Build(now.Date.AddDays(-1), now.Date.AddDays(-1).Add(now.TimeOfDay));
        var last14 = _reports.Build(now.Date.AddDays(-13), now.Date.AddDays(1));
        var last30 = _reports.Build(now.Date.AddDays(-29), now.Date.AddDays(1));
        var hall = _hall.GetHallState(now);

        var occupied = hall.Count(t => t.IsOccupied);
        var guestsNow = hall.Where(t => t.IsOccupied).Sum(t => t.OpenOrder!.GuestsCount);
        var openSum = hall.Where(t => t.IsOccupied).Sum(t => t.OpenOrder!.Total);

        Kpis = new[]
        {
            new KpiCard("Виручка сьогодні", Format.Money(today.Revenue),
                Trend(today.Revenue, yesterday.Revenue, "до вчора"), MaterialIconKind.CashMultiple,
                Tones.Accent, Tones.Soft(Tones.AccentColor), yesterday.Revenue == 0 ? null : today.Revenue >= yesterday.Revenue),
            new KpiCard("Закритих чеків", today.OrdersCount.ToString(),
                $"Середній чек {Format.Money(today.AverageCheck)}", MaterialIconKind.ReceiptTextCheckOutline,
                Tones.Green, Tones.Soft(Tones.GreenColor), null),
            new KpiCard("Столи зайнято", $"{occupied} / {hall.Count}",
                $"{Format.Plural(guestsNow, "гість", "гості", "гостей")} у залі", MaterialIconKind.TableChair,
                Tones.Blue, Tones.Soft(Tones.BlueColor), null),
            new KpiCard("Відкриті рахунки", Format.Money(openSum),
                "Очікується до оплати", MaterialIconKind.ClockOutline,
                Tones.Purple, Tones.Soft(Tones.PurpleColor), null),
        };

        RevenueChart = last14.Daily
            .Select(d => new ChartPoint(d.Date.ToString("dd.MM"), (double)d.Revenue,
                $"{Format.DayName(d.Date)}, {d.Date:dd.MM} · {Format.Money(d.Revenue)}"))
            .ToList();
        Revenue14Text = Format.Money(last14.Revenue);

        HourlyChart = Enumerable.Range(10, 14)
            .Select(h => new ChartPoint($"{h}", last30.OrdersByHour[h],
                $"{h}:00 · {Format.Plural(last30.OrdersByHour[h], "чек", "чеки", "чеків")}"))
            .ToList();

        CategorySegments = last30.Categories
            .Select(c => new DonutSegment(c.Name, (double)c.Revenue, Color.Parse(c.Color), Format.Money(c.Revenue)))
            .ToList();
        CategoryTotalText = Format.Money(last30.Revenue);

        var topMax = last30.TopDishes.FirstOrDefault()?.Quantity ?? 1;
        TopDishes = last30.TopDishes.Take(5)
            .Select((d, i) => new RankedRow(i + 1, d.Name, d.Category, $"{d.Quantity} шт", (double)d.Quantity / topMax))
            .ToList();

        var queue = _orders.GetKitchenQueue();
        KitchenNew = queue.Count(i => i.Status == OrderItemStatus.New);
        KitchenCooking = queue.Count(i => i.Status == OrderItemStatus.Cooking);
        KitchenReady = queue.Count(i => i.Status == OrderItemStatus.Ready);

        LowStock = _inventory.GetIngredients()
            .Where(i => i.IsLow)
            .OrderBy(i => i.MinQuantity == 0 ? 1 : i.Quantity / i.MinQuantity)
            .Take(5)
            .Select(i => new AlertRow(i.Name, $"{Format.Number(i.Quantity)} / {Format.Number(i.MinQuantity)} {i.Unit}",
                i.IsCritical ? Tones.Red : Tones.Yellow,
                Tones.Soft(i.IsCritical ? Tones.RedColor : Tones.YellowColor),
                i.IsCritical ? MaterialIconKind.AlertOctagonOutline : MaterialIconKind.AlertOutline))
            .ToList();

        UpcomingReservations = _reservations.GetForDay(now)
            .Concat(_reservations.GetForDay(now.AddDays(1)))
            .Where(r => r.Status == ReservationStatus.Active && r.StartsAt >= now.AddMinutes(-30))
            .Take(4)
            .Select((r, i) => new RankedRow(i + 1, r.GuestName,
                $"Стіл №{r.Table?.Number} · {Format.Plural(r.GuestsCount, "гість", "гості", "гостей")}",
                r.StartsAt.Date == now.Date ? r.StartsAt.ToString("HH:mm") : "завтра " + r.StartsAt.ToString("HH:mm"), 0))
            .ToList();

        OnPropertyChanged(string.Empty);
    }

    private static string Trend(decimal now, decimal before, string suffix)
    {
        if (before == 0) return "Немає даних для порівняння";
        var delta = (now - before) / before * 100m;
        return $"{(delta >= 0 ? "↑" : "↓")} {Math.Abs(delta):0} % {suffix}";
    }

    [RelayCommand]
    private void OpenKitchen() => _navigation.Navigate<KitchenViewModel>();

    [RelayCommand]
    private void OpenInventory() => _navigation.Navigate<InventoryViewModel>();

    [RelayCommand]
    private void OpenReservations() => _navigation.Navigate<ReservationsViewModel>();

    [RelayCommand]
    private void OpenHall() => _navigation.Navigate<HallViewModel>();
}
