using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed partial class DayChip : ObservableObject
{
    public DayChip(DateTime date, int count, Action<DayChip> select)
    {
        Date = date;
        Count = count;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public DateTime Date { get; }
    public int Count { get; }
    public bool HasCount => Count > 0;
    public string DayName => Date.Date == DateTime.Today ? "Сьогодні" : Format.DayName(Date);
    public string DayNumber => Date.Day.ToString();
    public string MonthName => Date.ToString("MMM", Format.Uk).TrimEnd('.');
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class ReservationCard
{
    public ReservationCard(Reservation r, Action<ReservationCard> edit, Action<ReservationCard> arrived, Action<ReservationCard> cancel)
    {
        Reservation = r;
        EditCommand = new RelayCommand(() => edit(this));
        ArrivedCommand = new RelayCommand(() => arrived(this));
        CancelCommand = new RelayCommand(() => cancel(this));
    }

    public Reservation Reservation { get; }
    public string TimeText => Reservation.StartsAt.ToString("HH:mm");
    public string EndText => "до " + Reservation.EndsAt.ToString("HH:mm");
    public string GuestName => Reservation.GuestName;
    public string Phone => Reservation.Phone;
    public string GuestsText => Format.Plural(Reservation.GuestsCount, "гість", "гості", "гостей");
    public string TableText => $"Стіл №{Reservation.Table?.Number} · {Reservation.Table?.Zone}";
    public string Note => Reservation.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Reservation.Note);
    public bool IsActive => Reservation.Status == ReservationStatus.Active;
    public bool IsClosed => !IsActive;
    public string StatusText => Reservation.Status.ToText();

    public IBrush Tone => Reservation.Status switch
    {
        ReservationStatus.Active => Tones.Blue,
        ReservationStatus.Arrived => Tones.Green,
        _ => Tones.Red
    };

    public IBrush ToneSoft => Tones.Soft(Reservation.Status switch
    {
        ReservationStatus.Active => Tones.BlueColor,
        ReservationStatus.Arrived => Tones.GreenColor,
        _ => Tones.RedColor
    });

    public IRelayCommand EditCommand { get; }
    public IRelayCommand ArrivedCommand { get; }
    public IRelayCommand CancelCommand { get; }
}

public sealed record TableOption(int Id, int Number, int Seats, string Zone)
{
    public string Title => $"№{Number} · {Zone} · {Format.Plural(Seats, "місце", "місця", "місць")}";
    public override string ToString() => Title;
}

public sealed record DurationOption(int Minutes, string Title)
{
    public override string ToString() => Title;
}

/// <summary>Бронювання: календарна стрічка, список на день, форма.</summary>
public sealed partial class ReservationsViewModel : PageViewModel
{
    private const int DaysAhead = 14;
    private readonly ReservationService _reservations;
    private readonly HallService _hall;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
    private List<TableOption> _allTables = new();

    public ReservationsViewModel(ToastService toasts, ReservationService reservations, HallService hall,
        NavigationService navigation, DialogService dialogs) : base(toasts)
    {
        _reservations = reservations;
        _hall = hall;
        _navigation = navigation;
        _dialogs = dialogs;

        for (var t = new TimeSpan(10, 0, 0); t <= new TimeSpan(22, 0, 0); t += TimeSpan.FromMinutes(30))
            TimeSlots.Add(t.ToString(@"hh\:mm"));
        Durations = new[]
        {
            new DurationOption(60, "1 година"), new DurationOption(90, "1,5 години"),
            new DurationOption(120, "2 години"), new DurationOption(180, "3 години"), new DurationOption(240, "4 години")
        };
    }

    public override string Title => "Бронювання";
    public override string Subtitle => "Резерв столів на найближчі два тижні";

    public ObservableCollection<DayChip> Days { get; } = new();
    public ObservableCollection<ReservationCard> Cards { get; } = new();
    public ObservableCollection<TableOption> TableOptions { get; } = new();
    public ObservableCollection<string> TimeSlots { get; } = new();
    public IReadOnlyList<DurationOption> Durations { get; }

    public DateTime SelectedDate => Days.FirstOrDefault(d => d.IsSelected)?.Date ?? DateTime.Today;
    public string SelectedDateText => Format.LongDate(SelectedDate);
    public string DaySummary
    {
        get
        {
            var active = Cards.Where(c => c.IsActive).ToList();
            return active.Count == 0
                ? "Немає активних бронювань"
                : $"{Format.Plural(active.Count, "бронювання", "бронювання", "бронювань")} · " +
                  Format.Plural(active.Sum(c => c.Reservation.GuestsCount), "гість", "гості", "гостей");
        }
    }
    public bool IsDayEmpty => Cards.Count == 0;

    // ---- Форма ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsEditing))]
    private int _editId;

    [ObservableProperty] private string _guestName = string.Empty;
    [ObservableProperty] private string _phone = "+380 ";
    [ObservableProperty] private decimal? _guests = 2;
    [ObservableProperty] private string? _time = "19:00";
    [ObservableProperty] private DurationOption? _duration;
    [ObservableProperty] private TableOption? _table;
    [ObservableProperty] private string _note = string.Empty;

    partial void OnGuestsChanged(decimal? value) => FilterTables();

    public string FormTitle => EditId == 0 ? "Нове бронювання" : "Редагування";
    public bool IsEditing => EditId != 0;

    public override void OnActivated()
    {
        _allTables = _hall.GetTables().Select(t => new TableOption(t.Id, t.Number, t.Seats, t.Zone)).ToList();
        BuildDays(DateTime.Today);
        ResetForm();
    }

    private void BuildDays(DateTime select)
    {
        var counts = _reservations.CountByDay(DateTime.Today, DaysAhead);
        Days.Clear();
        for (var i = 0; i < DaysAhead; i++)
        {
            var date = DateTime.Today.AddDays(i);
            Days.Add(new DayChip(date, counts.GetValueOrDefault(date), SelectDay) { IsSelected = date == select.Date });
        }
        if (!Days.Any(d => d.IsSelected)) Days[0].IsSelected = true;
        LoadDay();
    }

    private void SelectDay(DayChip day)
    {
        foreach (var d in Days)
            d.IsSelected = ReferenceEquals(d, day);
        LoadDay();
    }

    private void LoadDay()
    {
        Cards.Clear();
        foreach (var r in _reservations.GetForDay(SelectedDate))
            Cards.Add(new ReservationCard(r, Edit, Arrived, Cancel));
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(SelectedDateText));
        OnPropertyChanged(nameof(DaySummary));
        OnPropertyChanged(nameof(IsDayEmpty));
    }

    private void FilterTables()
    {
        var guests = (int)(Guests ?? 1);
        var current = Table;
        TableOptions.Clear();
        foreach (var t in _allTables.Where(t => t.Seats >= guests))
            TableOptions.Add(t);
        Table = TableOptions.FirstOrDefault(t => t.Id == current?.Id) ?? TableOptions.FirstOrDefault();
    }

    [RelayCommand]
    private void ResetForm()
    {
        EditId = 0;
        GuestName = string.Empty;
        Phone = "+380 ";
        Guests = 2;
        Time = SelectedDate.Date == DateTime.Today
            ? TimeSlots.FirstOrDefault(s => TimeSpan.Parse(s) > DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(30))) ?? TimeSlots[^1]
            : "19:00";
        Duration = Durations[2];
        Note = string.Empty;
        FilterTables();
    }

    private void Edit(ReservationCard card)
    {
        var r = card.Reservation;
        if (r.Status != ReservationStatus.Active)
        {
            Toasts.Info("Бронювання вже закрито", "Редагувати можна лише активні бронювання");
            return;
        }
        EditId = r.Id;
        GuestName = r.GuestName;
        Phone = r.Phone;
        Guests = r.GuestsCount;
        Time = r.StartsAt.ToString("HH:mm");
        if (!TimeSlots.Contains(Time)) TimeSlots.Add(Time);
        Duration = Durations.FirstOrDefault(d => d.Minutes == r.DurationMinutes) ?? Durations[2];
        Note = r.Note;
        FilterTables();
        Table = TableOptions.FirstOrDefault(t => t.Id == r.TableId) ?? Table;
    }

    [RelayCommand]
    private void Save()
    {
        if (Table is null || Time is null)
        {
            Toasts.Warning("Оберіть стіл і час");
            return;
        }

        var reservation = new Reservation
        {
            Id = EditId,
            TableId = Table.Id,
            GuestName = GuestName,
            Phone = Phone,
            GuestsCount = (int)(Guests ?? 0),
            StartsAt = SelectedDate.Date + TimeSpan.Parse(Time),
            DurationMinutes = Duration?.Minutes ?? 120,
            Note = Note
        };

        var wasNew = EditId == 0;
        if (Try(() => _reservations.Save(reservation),
                wasNew ? "Стіл заброньовано" : "Бронювання оновлено",
                $"{reservation.GuestName}, {reservation.StartsAt:dd.MM HH:mm}, стіл №{Table.Number}"))
        {
            BuildDays(SelectedDate);
            ResetForm();
        }
    }

    private void Arrived(ReservationCard card)
    {
        var r = card.Reservation;
        if (!Try(() => _reservations.SetStatus(r.Id, ReservationStatus.Arrived), "Гостей зустріли", r.GuestName))
            return;
        if (r.StartsAt.Date == DateTime.Today)
            _navigation.Navigate<OrderViewModel>(vm => vm.Load(r.TableId, r.GuestsCount));
        else
            BuildDays(SelectedDate);
    }

    private async void Cancel(ReservationCard card)
    {
        var r = card.Reservation;
        if (!await _dialogs.ConfirmAsync("Скасувати бронювання?",
                $"{r.GuestName}, {r.StartsAt:dd.MM о HH:mm}, стіл №{r.Table?.Number}", "Скасувати бронювання", danger: true))
            return;
        if (Try(() => _reservations.SetStatus(r.Id, ReservationStatus.Cancelled), "Бронювання скасовано", r.GuestName))
        {
            if (EditId == r.Id) ResetForm();
            BuildDays(SelectedDate);
        }
    }
}
