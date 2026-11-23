using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed class StockRow : ObservableObject
{
    public StockRow(Ingredient ingredient, Action<StockRow> select)
    {
        Ingredient = ingredient;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public Ingredient Ingredient { get; }
    public string Name => Ingredient.Name;
    public string QuantityText => $"{Format.Number(Ingredient.Quantity)} {Ingredient.Unit}";
    public string MinText => $"мін. {Format.Number(Ingredient.MinQuantity)} {Ingredient.Unit}";
    public string CostText => $"{Format.Money(Ingredient.CostPerUnit)}/{Ingredient.Unit}";
    public string ValueText => Format.Money(Ingredient.Quantity * Ingredient.CostPerUnit);

    /// <summary>Заповненість відносно «комфортного» запасу (3× мінімум).</summary>
    public double Level => Ingredient.MinQuantity <= 0 ? 1 : (double)(Ingredient.Quantity / (Ingredient.MinQuantity * 3));

    public string StatusText => Ingredient.IsCritical ? "Критично" : Ingredient.IsLow ? "Мало" : "Норма";
    public IBrush Tone => Ingredient.IsCritical ? Tones.Red : Ingredient.IsLow ? Tones.Yellow : Tones.Green;
    public IBrush ToneSoft => Tones.Soft(Ingredient.IsCritical ? Tones.RedColor : Ingredient.IsLow ? Tones.YellowColor : Tones.GreenColor);
    public IRelayCommand SelectCommand { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed partial class StockFilter : ObservableObject
{
    public StockFilter(string title, Func<Ingredient, bool> predicate, Action<StockFilter> select)
    {
        Title = title;
        Predicate = predicate;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Title { get; }
    public Func<Ingredient, bool> Predicate { get; }
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Склад: залишки, поповнення, редагування інгредієнтів.</summary>
public sealed partial class InventoryViewModel : PageViewModel
{
    private readonly InventoryService _inventory;
    private readonly DialogService _dialogs;
    private List<StockRow> _all = new();

    public InventoryViewModel(ToastService toasts, InventoryService inventory, DialogService dialogs) : base(toasts)
    {
        _inventory = inventory;
        _dialogs = dialogs;
        Filters = new[]
        {
            new StockFilter("Усі", _ => true, SelectFilter),
            new StockFilter("Закінчуються", i => i.IsLow, SelectFilter),
            new StockFilter("Критично", i => i.IsCritical, SelectFilter),
        };
        Filters[0].IsSelected = true;
    }

    public override string Title => "Склад";
    public override string Subtitle => "Залишки продуктів списуються автоматично за технологічними картами";

    public IReadOnlyList<StockFilter> Filters { get; }
    public ObservableCollection<StockRow> Rows { get; } = new();
    public IReadOnlyList<string> Units { get; } = new[] { "кг", "л", "шт", "уп" };

    [ObservableProperty]
    private string _search = string.Empty;

    partial void OnSearchChanged(string value) => ApplyFilter();

    public int TotalCount => _all.Count;
    public int LowCount => _all.Count(r => r.Ingredient.IsLow);
    public int CriticalCount => _all.Count(r => r.Ingredient.IsCritical);
    public string StockValueText => Format.Money(_all.Sum(r => r.Ingredient.Quantity * r.Ingredient.CostPerUnit));

    // ---- Форма ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorTitle), nameof(IsExisting))]
    private int _editId;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editUnit = "кг";
    [ObservableProperty] private decimal? _editQuantity;
    [ObservableProperty] private decimal? _editMin;
    [ObservableProperty] private decimal? _editCost;
    [ObservableProperty] private decimal? _restockAmount;

    public string EditorTitle => EditId == 0 ? "Новий інгредієнт" : EditName;
    public bool IsExisting => EditId != 0;

    public override void OnActivated() => Load();

    private void Load(int? selectId = null)
    {
        _all = _inventory.GetIngredients().Select(i => new StockRow(i, Select)).ToList();
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(LowCount));
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(StockValueText));

        var row = _all.FirstOrDefault(r => r.Ingredient.Id == (selectId ?? EditId))
                  ?? _all.Where(r => r.Ingredient.IsLow).OrderBy(r => r.Level).FirstOrDefault()
                  ?? _all.FirstOrDefault();
        if (row is not null) Select(row);
        else New();
    }

    private void SelectFilter(StockFilter filter)
    {
        foreach (var f in Filters)
            f.IsSelected = ReferenceEquals(f, filter);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = Filters.First(f => f.IsSelected);
        var term = Search.Trim();
        Rows.Clear();
        foreach (var r in _all
                     .Where(r => filter.Predicate(r.Ingredient))
                     .Where(r => term.Length == 0 || r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            Rows.Add(r);
    }

    private void Select(StockRow row)
    {
        foreach (var r in _all)
            r.IsSelected = ReferenceEquals(r, row);
        var i = row.Ingredient;
        EditId = i.Id;
        EditName = i.Name;
        EditUnit = i.Unit;
        EditQuantity = i.Quantity;
        EditMin = i.MinQuantity;
        EditCost = i.CostPerUnit;
        RestockAmount = i.IsLow ? Math.Ceiling(i.MinQuantity * 3 - i.Quantity) : null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private void New()
    {
        foreach (var r in _all)
            r.IsSelected = false;
        EditId = 0;
        EditName = string.Empty;
        EditUnit = "кг";
        EditQuantity = 0;
        EditMin = 1;
        EditCost = null;
        RestockAmount = null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private void Save()
    {
        var ingredient = new Ingredient
        {
            Id = EditId,
            Name = EditName,
            Unit = EditUnit,
            Quantity = EditQuantity ?? 0,
            MinQuantity = EditMin ?? 0,
            CostPerUnit = EditCost ?? 0
        };
        Ingredient? saved = null;
        if (Try(() => saved = _inventory.Save(ingredient), "Збережено", ingredient.Name))
            Load(saved!.Id);
    }

    [RelayCommand]
    private void Restock()
    {
        var amount = RestockAmount ?? 0;
        if (Try(() => _inventory.Restock(EditId, amount), "Склад поповнено", $"{EditName}: +{Format.Number(amount)} {EditUnit}"))
            Load(EditId);
    }

    [RelayCommand]
    private void QuickAmount(string amount) => RestockAmount = (RestockAmount ?? 0) + decimal.Parse(amount);

    [RelayCommand]
    private async Task Delete()
    {
        if (EditId == 0) return;
        if (!await _dialogs.ConfirmAsync("Видалити інгредієнт?", $"«{EditName}» буде видалено зі складу.", "Видалити", danger: true))
            return;
        if (Try(() => _inventory.Delete(EditId), "Інгредієнт видалено", EditName))
        {
            EditId = 0;
            Load();
        }
    }
}
