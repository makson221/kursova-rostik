using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;
using RestaurantSystem.ViewModels.Dialogs;

namespace RestaurantSystem.ViewModels;

public sealed partial class CategoryChip : ObservableObject
{
    public CategoryChip(int? id, string title, MaterialIconKind icon, IBrush tone, Action<CategoryChip> select)
    {
        Id = id;
        Title = title;
        Icon = icon;
        Tone = tone;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public int? Id { get; }
    public string Title { get; }
    public MaterialIconKind Icon { get; }
    public IBrush Tone { get; }
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Картка страви в POS-меню.</summary>
public sealed partial class DishCard : ObservableObject
{
    public DishCard(Dish dish, Action<DishCard> add)
    {
        Dish = dish;
        Tone = Tones.Solid(dish.Category?.Color ?? "#F2A65A");
        ToneSoft = Tones.Soft(Color.Parse(dish.Category?.Color ?? "#F2A65A"), 0x22);
        Icon = MaterialIconParser.Parse(dish.Category?.Icon);
        AddCommand = new RelayCommand(() => add(this));
    }

    public Dish Dish { get; }
    public string Name => Dish.Name;
    public string Portion => Dish.Portion;
    public string PriceText => Format.Money(Dish.Price);
    public string CookTimeText => $"{Dish.CookTimeMinutes} хв";
    public bool IsAvailable => Dish.IsAvailable;
    public IBrush Tone { get; }
    public IBrush ToneSoft { get; }
    public MaterialIconKind Icon { get; }
    public IRelayCommand AddCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InOrder))]
    private int _countInOrder;

    public bool InOrder => CountInOrder > 0;
}

/// <summary>Рядок чеку в POS.</summary>
public sealed partial class OrderLine : ObservableObject
{
    public OrderLine(OrderItem item, OrderViewModel owner)
    {
        Item = item;
        _noteDraft = item.Note;
        IncreaseCommand = new RelayCommand(() => owner.ChangeQuantity(this, 1));
        DecreaseCommand = new RelayCommand(() => owner.ChangeQuantity(this, -1));
        RemoveCommand = new RelayCommand(() => owner.Remove(this));
        ToggleNoteCommand = new RelayCommand(() => IsEditingNote = !IsEditingNote);
        SaveNoteCommand = new RelayCommand(() => owner.SaveNote(this));
    }

    public OrderItem Item { get; }
    public string Name => Item.Dish?.Name ?? "—";
    public string Note => Item.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Item.Note);
    public int Quantity => Item.Quantity;
    public string PriceText => $"{Item.Quantity} × {Format.Money(Item.Price)}";
    public string TotalText => Format.Money(Item.LineTotal);
    public bool IsDraft => Item.Status == OrderItemStatus.Draft;
    public bool IsLocked => !IsDraft;
    public string StatusText => Item.Status.ToText();

    // Кольори статусів, підібрані для «паперового» чеку (світлий фон).
    private Color PaperColor => Item.Status switch
    {
        OrderItemStatus.New => Color.Parse("#2F6FD0"),
        OrderItemStatus.Cooking => Color.Parse("#C9621E"),
        OrderItemStatus.Ready => Color.Parse("#2E9D62"),
        _ => Color.Parse("#8A7765")
    };

    public IBrush StatusTone => new Avalonia.Media.Immutable.ImmutableSolidColorBrush(PaperColor);
    public IBrush StatusSoft => Tones.Soft(PaperColor, 0x24);

    public IRelayCommand IncreaseCommand { get; }
    public IRelayCommand DecreaseCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand ToggleNoteCommand { get; }
    public IRelayCommand SaveNoteCommand { get; }

    [ObservableProperty]
    private bool _isEditingNote;

    [ObservableProperty]
    private string _noteDraft;
}

/// <summary>POS-екран офіціанта: меню + поточний чек столу.</summary>
public sealed partial class OrderViewModel : PageViewModel
{
    private readonly OrderService _orders;
    private readonly MenuService _menu;
    private readonly AuthService _auth;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
    private List<DishCard> _allDishes = new();
    private int _orderId;

    public OrderViewModel(ToastService toasts, OrderService orders, MenuService menu, AuthService auth,
        NavigationService navigation, DialogService dialogs) : base(toasts)
    {
        _orders = orders;
        _menu = menu;
        _auth = auth;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public override string Title => Order is null ? "Замовлення" : $"Стіл №{Order.Table?.Number}";
    public override string Subtitle => Order is null ? string.Empty
        : $"{Order.Table?.Zone} · відкрито о {Order.CreatedAt:HH:mm} · офіціант {Order.Waiter?.FullName}";
    public override Type NavTarget => typeof(HallViewModel);

    public Order? Order { get; private set; }

    public ObservableCollection<CategoryChip> Categories { get; } = new();
    public ObservableCollection<DishCard> Dishes { get; } = new();
    public ObservableCollection<OrderLine> Lines { get; } = new();

    [ObservableProperty]
    private string _search = string.Empty;

    partial void OnSearchChanged(string value) => ApplyFilter();

    public string GuestsText => Order is null ? "—" : Format.Plural(Order.GuestsCount, "гість", "гості", "гостей");
    public string SubtotalText => Format.MoneyExact(Order?.Subtotal ?? 0);
    public string ItemsCountText => Format.Plural(Order?.Items.Sum(i => i.Quantity) ?? 0, "позиція", "позиції", "позицій");
    public int DraftCount => Order?.Items.Where(i => i.Status == OrderItemStatus.Draft).Sum(i => i.Quantity) ?? 0;
    public bool HasDrafts => DraftCount > 0;
    public bool HasLines => Lines.Count > 0;
    public bool IsEmpty => Lines.Count == 0;
    public string SendText => HasDrafts ? $"На кухню · {DraftCount}" : "На кухню";

    /// <summary>Відкриває рахунок столу (або створює новий).</summary>
    public void Load(int tableId, int guests)
    {
        try
        {
            var order = _orders.GetOpenOrderForTable(tableId);
            if (order is null)
            {
                var created = _orders.OpenOrder(tableId, _auth.RequireUser().Id, guests);
                order = _orders.GetOrder(created.Id);
            }
            _orderId = order.Id;
            LoadMenu();
            Reload(order);
        }
        catch (BusinessException ex)
        {
            Toasts.Warning(ex.Message);
        }
    }

    public override void OnDeactivated()
    {
        // Порожній рахунок не залишаємо — інакше стіл «зависне» зайнятим.
        if (Order is { Status: OrderStatus.Open } && Order.Items.Count == 0)
        {
            try { _orders.Cancel(Order.Id); }
            catch (BusinessException) { /* нічого страшного */ }
        }
    }

    private void LoadMenu()
    {
        var categories = _menu.GetCategories();
        Categories.Clear();
        Categories.Add(new CategoryChip(null, "Усе меню", MaterialIconKind.SilverwareForkKnife, Tones.Accent, SelectCategory)
            { IsSelected = true });
        foreach (var c in categories)
            Categories.Add(new CategoryChip(c.Id, c.Name, MaterialIconParser.Parse(c.Icon), Tones.Solid(c.Color), SelectCategory));

        _allDishes = _menu.GetDishes().Select(d => new DishCard(d, AddDish)).ToList();
        ApplyFilter();
    }

    private void SelectCategory(CategoryChip chip)
    {
        foreach (var c in Categories)
            c.IsSelected = ReferenceEquals(c, chip);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var categoryId = Categories.FirstOrDefault(c => c.IsSelected)?.Id;
        var term = Search.Trim();
        Dishes.Clear();
        foreach (var d in _allDishes
                     .Where(d => categoryId is null || d.Dish.CategoryId == categoryId)
                     .Where(d => term.Length == 0 || d.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                     .OrderByDescending(d => d.IsAvailable))
            Dishes.Add(d);
    }

    private void Reload(Order? order = null)
    {
        Order = order ?? _orders.GetOrder(_orderId);
        Lines.Clear();
        foreach (var item in Order.Items.OrderBy(i => i.Status == OrderItemStatus.Draft).ThenBy(i => i.Id))
            Lines.Add(new OrderLine(item, this));

        foreach (var card in _allDishes)
            card.CountInOrder = Order.Items.Where(i => i.DishId == card.Dish.Id).Sum(i => i.Quantity);

        OnPropertyChanged(string.Empty);
    }

    private void AddDish(DishCard card)
    {
        if (Try(() => _orders.AddDish(_orderId, card.Dish.Id)))
            Reload();
    }

    internal void ChangeQuantity(OrderLine line, int delta)
    {
        if (Try(() => _orders.ChangeQuantity(line.Item.Id, delta)))
            Reload();
    }

    internal void Remove(OrderLine line)
    {
        if (Try(() => _orders.RemoveItem(line.Item.Id)))
            Reload();
    }

    internal void SaveNote(OrderLine line)
    {
        if (Try(() => _orders.SetNote(line.Item.Id, line.NoteDraft ?? string.Empty)))
            Reload();
    }

    [RelayCommand]
    private void ChangeGuests(string delta)
    {
        if (Order is null) return;
        var value = Order.GuestsCount + int.Parse(delta);
        if (value < 1) return;
        if (Try(() => _orders.SetGuests(_orderId, value)))
            Reload();
    }

    [RelayCommand]
    private void SendToKitchen()
    {
        var sent = 0;
        var lowStock = new List<string>();
        if (!Try(() => (sent, lowStock) = _orders.SendToKitchen(_orderId)))
            return;

        Toasts.Success("Передано на кухню", Format.Plural(sent, "позиція", "позиції", "позицій"));
        if (lowStock.Count > 0)
            Toasts.Warning("Закінчуються продукти", string.Join(", ", lowStock));
        Reload();
    }

    [RelayCommand]
    private async Task Pay()
    {
        if (Order is null || Order.Items.Count == 0)
        {
            Toasts.Info("Чек порожній", "Додайте страви з меню");
            return;
        }

        var payment = await _dialogs.ShowAsync(new PaymentDialogViewModel(Order));
        if (payment is null) return;

        Order? paid = null;
        if (!Try(() => paid = _orders.Pay(_orderId, payment.Method, payment.DiscountPercent)))
            return;

        Toasts.Success($"Стіл №{paid!.Table?.Number} розраховано", Format.MoneyExact(paid.Total));
        Order = paid;
        await _dialogs.ShowAsync(new ReceiptDialogViewModel(paid, Toasts));
        _navigation.Navigate<HallViewModel>();
    }

    [RelayCommand]
    private async Task CancelOrder()
    {
        if (Order is null) return;
        if (Order.Items.Any(i => i.Status != OrderItemStatus.Draft))
        {
            Toasts.Warning("Частину замовлення вже передано на кухню", "Скасувати можна лише неподане замовлення");
            return;
        }

        if (!await _dialogs.ConfirmAsync("Скасувати замовлення?",
                $"Рахунок столу №{Order.Table?.Number} буде закрито без оплати.", "Скасувати замовлення", danger: true))
            return;

        if (Try(() => _orders.Cancel(_orderId), "Замовлення скасовано"))
            _navigation.Navigate<HallViewModel>();
    }

    [RelayCommand]
    private void Back() => _navigation.Navigate<HallViewModel>();
}
