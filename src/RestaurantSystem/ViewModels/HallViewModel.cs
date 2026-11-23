using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public enum TableVisualState
{
    Free,
    Occupied,
    Reserved
}

/// <summary>Стілець навколо столу на плані (координати всередині контейнера столу).</summary>
public sealed record ChairShape(double X, double Y, double Width, double Height);

public sealed partial class ZoneFilter : ObservableObject
{
    public ZoneFilter(string title, string? zone, Action<ZoneFilter> select)
    {
        Title = title;
        Zone = zone;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Title { get; }
    public string? Zone { get; }
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Стіл на інтерактивному плані зали.</summary>
public sealed partial class TableItemViewModel : ObservableObject
{
    public const double ChairGap = 16;

    public TableItemViewModel(TableState state, Action<TableItemViewModel> select)
    {
        Table = state.Table;
        SelectCommand = new RelayCommand(() => select(this));

        (TableWidth, TableHeight) = Table.Shape switch
        {
            TableShape.Round => Table.Seats <= 2 ? (70d, 70d) : (92d, 92d),
            TableShape.Square => (88d, 88d),
            _ => Table.Seats <= 6 ? (150d, 78d) : (192d, 78d)
        };
        Chairs = BuildChairs();
        Update(state);
    }

    public RestaurantTable Table { get; }
    public int Number => Table.Number;
    public string Zone => Table.Zone;
    public double X => Table.X;
    public double Y => Table.Y;
    public double TableWidth { get; }
    public double TableHeight { get; }
    public double Width => TableWidth + ChairGap * 2;
    public double Height => TableHeight + ChairGap * 2;
    public Avalonia.CornerRadius CornerRadius => new(Table.Shape == TableShape.Round ? TableWidth / 2 : 16);
    public IReadOnlyList<ChairShape> Chairs { get; }
    public IRelayCommand SelectCommand { get; }
    public string SeatsText => Format.Plural(Table.Seats, "місце", "місця", "місць");

    public Order? Order { get; private set; }
    public Reservation? Reservation { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFree), nameof(IsOccupied), nameof(IsReserved), nameof(StatusText))]
    private TableVisualState _state;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDimmed;

    [ObservableProperty]
    private string _line1 = string.Empty;

    [ObservableProperty]
    private string _line2 = string.Empty;

    public bool IsFree => State == TableVisualState.Free;
    public bool IsOccupied => State == TableVisualState.Occupied;
    public bool IsReserved => State == TableVisualState.Reserved;

    public string StatusText => State switch
    {
        TableVisualState.Occupied => "Зайнятий",
        TableVisualState.Reserved => "Заброньований",
        _ => "Вільний"
    };

    public void Update(TableState state)
    {
        Order = state.OpenOrder;
        Reservation = state.UpcomingReservation;
        State = state.IsOccupied ? TableVisualState.Occupied
            : state.IsReserved ? TableVisualState.Reserved
            : TableVisualState.Free;

        if (Order is not null)
        {
            Line1 = Format.Money(Order.Total);
            Line2 = Format.Minutes(DateTime.Now - Order.CreatedAt);
        }
        else if (Reservation is not null)
        {
            Line1 = Reservation.StartsAt.ToString("HH:mm");
            Line2 = Format.Plural(Reservation.GuestsCount, "гість", "гості", "гостей");
        }
        else
        {
            Line1 = SeatsText;
            Line2 = string.Empty;
        }
    }

    private List<ChairShape> BuildChairs()
    {
        const double thick = 9, len = 24;
        var chairs = new List<ChairShape>();
        double cx = Width / 2, cy = Height / 2;

        if (Table.Shape == TableShape.Round)
        {
            var r = TableWidth / 2 + 8;
            for (var i = 0; i < Table.Seats; i++)
            {
                var a = Math.PI * 2 * i / Table.Seats - Math.PI / 2 + (Table.Seats == 2 ? Math.PI / 2 : Math.PI / 4);
                var px = cx + r * Math.Cos(a);
                var py = cy + r * Math.Sin(a);
                chairs.Add(new ChairShape(px - 8, py - 8, 16, 16));
            }
            return chairs;
        }

        // Прямокутні столи: розставляємо стільці по довгих сторонах та торцях.
        var ends = Table.Seats >= 6 ? 2 : Table.Shape == TableShape.Square ? 2 : 0;
        var perSide = (Table.Seats - ends) / 2;
        for (var i = 0; i < perSide; i++)
        {
            var step = TableWidth / perSide;
            var x = ChairGap + step * i + step / 2 - len / 2;
            chairs.Add(new ChairShape(x, ChairGap - thick - 3, len, thick));
            chairs.Add(new ChairShape(x, ChairGap + TableHeight + 3, len, thick));
        }
        if (ends > 0)
        {
            chairs.Add(new ChairShape(ChairGap - thick - 3, cy - len / 2, thick, len));
            chairs.Add(new ChairShape(ChairGap + TableWidth + 3, cy - len / 2, thick, len));
        }
        return chairs;
    }
}

/// <summary>Інтерактивний план зали зі станом столів.</summary>
public sealed partial class HallViewModel : PageViewModel
{
    private readonly HallService _hall;
    private readonly ReservationService _reservations;
    private readonly NavigationService _navigation;
    private readonly DispatcherTimer _timer;

    public HallViewModel(ToastService toasts, HallService hall, ReservationService reservations, NavigationService navigation)
        : base(toasts)
    {
        _hall = hall;
        _reservations = reservations;
        _navigation = navigation;

        Zones = new[]
        {
            new ZoneFilter("Усі зони", null, SelectZone),
            new ZoneFilter("Основна зала", "Основна зала", SelectZone),
            new ZoneFilter("Тераса", "Тераса", SelectZone),
            new ZoneFilter("VIP", "VIP", SelectZone),
        };
        Zones[0].IsSelected = true;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => Refresh();
    }

    public override string Title => "План зали";

    public IReadOnlyList<ZoneFilter> Zones { get; }
    public ObservableCollection<TableItemViewModel> Tables { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private TableItemViewModel? _selected;

    public bool HasSelection => Selected is not null;

    public int FreeCount => Tables.Count(t => t.IsFree);
    public int OccupiedCount => Tables.Count(t => t.IsOccupied);
    public int ReservedCount => Tables.Count(t => t.IsReserved);
    public string GuestsText => Format.Plural(Tables.Where(t => t.Order is not null).Sum(t => t.Order!.GuestsCount),
        "гість", "гості", "гостей");

    // ---- Деталі вибраного столу ----
    public string SelectedTitle => Selected is null ? string.Empty : $"Стіл №{Selected.Number}";
    public string SelectedSubtitle => Selected is null ? string.Empty : $"{Selected.Zone} · {Selected.SeatsText}";
    public string SelectedWaiter => Selected?.Order?.Waiter?.FullName ?? "—";
    public string SelectedGuests => Selected?.Order is { } o ? o.GuestsCount.ToString() : "—";
    public string SelectedTotal => Selected?.Order is { } o ? Format.MoneyExact(o.Total) : "—";
    public string SelectedOpened => Selected?.Order is { } o ? $"{o.CreatedAt:HH:mm} ({Format.Minutes(DateTime.Now - o.CreatedAt)})" : "—";
    public string SelectedItems => Selected?.Order is { } o ? Format.Plural(o.Items.Sum(i => i.Quantity), "позиція", "позиції", "позицій") : "—";
    public string SelectedReservation => Selected?.Reservation is { } r
        ? $"{r.StartsAt:HH:mm} · {r.GuestName} · {Format.Plural(r.GuestsCount, "гість", "гості", "гостей")}"
        : string.Empty;
    public string SelectedReservationNote => Selected?.Reservation?.Note ?? string.Empty;
    public bool SelectedHasReservation => Selected?.Reservation is not null;
    public bool SelectedHasOrder => Selected?.Order is not null;
    public bool SelectedCanOpen => Selected?.Order is null;

    public override void OnActivated()
    {
        Refresh();
        _timer.Start();
    }

    public override void OnDeactivated() => _timer.Stop();

    [RelayCommand]
    private void Refresh()
    {
        var states = _hall.GetHallState(DateTime.Now);
        if (Tables.Count == 0)
        {
            foreach (var s in states)
                Tables.Add(new TableItemViewModel(s, Select));
        }
        else
        {
            foreach (var s in states)
                Tables.FirstOrDefault(t => t.Table.Id == s.Table.Id)?.Update(s);
        }

        ApplyZone();
        RaiseSelectionChanged();
        OnPropertyChanged(nameof(FreeCount));
        OnPropertyChanged(nameof(OccupiedCount));
        OnPropertyChanged(nameof(ReservedCount));
        OnPropertyChanged(nameof(GuestsText));
    }

    private void SelectZone(ZoneFilter zone)
    {
        foreach (var z in Zones)
            z.IsSelected = ReferenceEquals(z, zone);
        ApplyZone();
    }

    private void ApplyZone()
    {
        var zone = Zones.First(z => z.IsSelected).Zone;
        foreach (var t in Tables)
            t.IsDimmed = zone is not null && t.Zone != zone;
    }

    private void Select(TableItemViewModel table)
    {
        if (ReferenceEquals(Selected, table))
        {
            OpenOrder();
            return;
        }
        foreach (var t in Tables)
            t.IsSelected = ReferenceEquals(t, table);
        Selected = table;
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        foreach (var name in new[]
                 {
                     nameof(SelectedTitle), nameof(SelectedSubtitle), nameof(SelectedWaiter), nameof(SelectedGuests),
                     nameof(SelectedTotal), nameof(SelectedOpened), nameof(SelectedItems), nameof(SelectedReservation),
                     nameof(SelectedReservationNote), nameof(SelectedHasReservation), nameof(SelectedHasOrder),
                     nameof(SelectedCanOpen)
                 })
            OnPropertyChanged(name);
    }

    [RelayCommand]
    private void OpenOrder()
    {
        if (Selected is null) return;
        var tableId = Selected.Table.Id;
        var guests = Selected.Reservation?.GuestsCount ?? Math.Min(2, Selected.Table.Seats);
        _navigation.Navigate<OrderViewModel>(vm => vm.Load(tableId, guests));
    }

    [RelayCommand]
    private void GuestsArrived()
    {
        if (Selected?.Reservation is not { } reservation) return;
        if (Try(() => _reservations.SetStatus(reservation.Id, ReservationStatus.Arrived), "Гостей зустріли", reservation.GuestName))
            OpenOrder();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var t in Tables)
            t.IsSelected = false;
        Selected = null;
        RaiseSelectionChanged();
    }
}
