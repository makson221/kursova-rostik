using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

/// <summary>Картка-тікет на кухонній дошці.</summary>
public sealed class KitchenTicket
{
    public KitchenTicket(OrderItem item, Action<KitchenTicket> advance)
    {
        Item = item;
        AdvanceCommand = new RelayCommand(() => advance(this));

        var since = item.Status == OrderItemStatus.Ready ? item.ReadyAt ?? item.SentAt : item.SentAt;
        Elapsed = DateTime.Now - (since ?? DateTime.Now);
        var cook = Math.Max(1, item.Dish?.CookTimeMinutes ?? 10);
        Progress = item.Status == OrderItemStatus.Cooking ? Math.Min(1, Elapsed.TotalMinutes / cook) : 0;
        IsOverdue = item.Status is OrderItemStatus.New or OrderItemStatus.Cooking && Elapsed.TotalMinutes > cook + 5
                    || item.Status == OrderItemStatus.Ready && Elapsed.TotalMinutes > 5;
    }

    public OrderItem Item { get; }
    public TimeSpan Elapsed { get; }
    public double Progress { get; }
    public bool IsOverdue { get; }

    public string TableText => $"№{Item.Order?.Table?.Number}";
    public string DishName => Item.Dish?.Name ?? "—";
    public string QuantityText => $"× {Item.Quantity}";
    public string Note => Item.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Item.Note);
    public string WaiterText => Item.Order?.Waiter?.FullName ?? string.Empty;
    public string ElapsedText => Format.Minutes(Elapsed);
    public string CookTimeText => $"норма {Item.Dish?.CookTimeMinutes} хв";
    public bool IsCooking => Item.Status == OrderItemStatus.Cooking;

    public IBrush TimeTone => IsOverdue ? Tones.Red : Tones.Muted;

    public string ActionText => Item.Status switch
    {
        OrderItemStatus.New => "Почати",
        OrderItemStatus.Cooking => "Готово",
        _ => "Видано"
    };

    public MaterialIconKind ActionIcon => Item.Status switch
    {
        OrderItemStatus.New => MaterialIconKind.Fire,
        OrderItemStatus.Cooking => MaterialIconKind.BellRingOutline,
        _ => MaterialIconKind.CheckAll
    };

    public IRelayCommand AdvanceCommand { get; }
}

/// <summary>Кухонний монітор — канбан-дошка з трьох колонок.</summary>
public sealed partial class KitchenViewModel : PageViewModel
{
    private readonly OrderService _orders;
    private readonly DispatcherTimer _timer;

    public KitchenViewModel(ToastService toasts, OrderService orders) : base(toasts)
    {
        _orders = orders;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => Refresh();
    }

    public override string Title => "Кухня";
    public override string Subtitle => "Черга приготування оновлюється автоматично";

    public ObservableCollection<KitchenTicket> NewTickets { get; } = new();
    public ObservableCollection<KitchenTicket> CookingTickets { get; } = new();
    public ObservableCollection<KitchenTicket> ReadyTickets { get; } = new();

    public int NewCount => NewTickets.Sum(t => t.Item.Quantity);
    public int CookingCount => CookingTickets.Sum(t => t.Item.Quantity);
    public int ReadyCount => ReadyTickets.Sum(t => t.Item.Quantity);
    public int OverdueCount => NewTickets.Concat(CookingTickets).Concat(ReadyTickets).Count(t => t.IsOverdue);
    public bool HasOverdue => OverdueCount > 0;
    public string UpdatedText { get; private set; } = string.Empty;

    public override void OnActivated()
    {
        Refresh();
        _timer.Start();
    }

    public override void OnDeactivated() => _timer.Stop();

    [RelayCommand]
    private void Refresh()
    {
        var queue = _orders.GetKitchenQueue();
        Fill(NewTickets, queue.Where(i => i.Status == OrderItemStatus.New));
        Fill(CookingTickets, queue.Where(i => i.Status == OrderItemStatus.Cooking));
        Fill(ReadyTickets, queue.Where(i => i.Status == OrderItemStatus.Ready).OrderBy(i => i.ReadyAt));
        UpdatedText = $"Оновлено о {DateTime.Now:HH:mm:ss}";

        OnPropertyChanged(nameof(NewCount));
        OnPropertyChanged(nameof(CookingCount));
        OnPropertyChanged(nameof(ReadyCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(HasOverdue));
        OnPropertyChanged(nameof(UpdatedText));
    }

    private void Fill(ObservableCollection<KitchenTicket> target, IEnumerable<OrderItem> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(new KitchenTicket(item, Advance));
    }

    private void Advance(KitchenTicket ticket)
    {
        OrderItemStatus status = default;
        if (!Try(() => status = _orders.AdvanceItem(ticket.Item.Id)))
            return;

        if (status == OrderItemStatus.Ready)
            Toasts.Success($"Готово для столу {ticket.TableText}", $"{ticket.DishName} {ticket.QuantityText}");
        Refresh();
    }
}
