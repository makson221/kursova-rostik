using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed partial class MenuCategoryItem : ObservableObject
{
    public MenuCategoryItem(Category? category, int count, Action<MenuCategoryItem> select)
    {
        Category = category;
        Count = count;
        Icon = category is null ? MaterialIconKind.SilverwareForkKnife : MaterialIconParser.Parse(category.Icon);
        Tone = Tones.Solid(category?.Color ?? "#F2A65A");
        SelectCommand = new RelayCommand(() => select(this));
    }

    public Category? Category { get; }
    public string Title => Category?.Name ?? "Усі страви";
    public int Count { get; }
    public MaterialIconKind Icon { get; }
    public IBrush Tone { get; }
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class MenuDishRow : ObservableObject
{
    public MenuDishRow(Dish dish, Action<MenuDishRow> select, Action<MenuDishRow> toggle)
    {
        Dish = dish;
        SelectCommand = new RelayCommand(() => select(this));
        ToggleCommand = new RelayCommand(() => toggle(this));
        FoodCost = dish.Ingredients.Sum(i => i.Amount * (i.Ingredient?.CostPerUnit ?? 0));
    }

    public Dish Dish { get; }
    public decimal FoodCost { get; }
    public string Name => Dish.Name;
    public string CategoryName => Dish.Category?.Name ?? string.Empty;
    public IBrush Tone => Tones.Solid(Dish.Category?.Color ?? "#F2A65A");
    public string PriceText => Format.Money(Dish.Price);
    public string Portion => Dish.Portion;
    public bool IsAvailable => Dish.IsAvailable;
    public bool IsUnavailable => !Dish.IsAvailable;
    public string CostText => FoodCost > 0
        ? $"собівартість {Format.Money(FoodCost)} · {(1 - FoodCost / Dish.Price) * 100:0} % маржа"
        : "без техкарти";
    public IRelayCommand SelectCommand { get; }
    public IRelayCommand ToggleCommand { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed partial class RecipeLine : ObservableObject
{
    public RecipeLine(IReadOnlyList<Ingredient> options, Action<RecipeLine> remove)
    {
        Options = options;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public IReadOnlyList<Ingredient> Options { get; }
    public IRelayCommand RemoveCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitText))]
    private Ingredient? _ingredient;

    [ObservableProperty]
    private decimal? _amount;

    public string UnitText => Ingredient?.Unit ?? string.Empty;
}

/// <summary>Редактор меню: категорії, страви, технологічні карти.</summary>
public sealed partial class MenuViewModel : PageViewModel
{
    private readonly MenuService _menu;
    private readonly InventoryService _inventory;
    private readonly DialogService _dialogs;
    private List<MenuDishRow> _allRows = new();
    private IReadOnlyList<Ingredient> _ingredients = Array.Empty<Ingredient>();

    public MenuViewModel(ToastService toasts, MenuService menu, InventoryService inventory, DialogService dialogs)
        : base(toasts)
    {
        _menu = menu;
        _inventory = inventory;
        _dialogs = dialogs;
    }

    public override string Title => "Меню ресторану";
    public override string Subtitle => "Страви, ціни та технологічні карти";

    public ObservableCollection<MenuCategoryItem> CategoryItems { get; } = new();
    public ObservableCollection<MenuDishRow> Rows { get; } = new();
    public ObservableCollection<Category> CategoryOptions { get; } = new();
    public ObservableCollection<RecipeLine> Recipe { get; } = new();

    [ObservableProperty]
    private string _search = string.Empty;

    partial void OnSearchChanged(string value) => ApplyFilter();

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    // ---- Форма редагування ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorTitle), nameof(IsExisting))]
    private int _editId;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private string _editPortion = string.Empty;
    [ObservableProperty] private decimal? _editPrice;
    [ObservableProperty] private decimal? _editCookTime = 10;
    [ObservableProperty] private bool _editAvailable = true;
    [ObservableProperty] private Category? _editCategory;

    public string EditorTitle => EditId == 0 ? "Нова страва" : "Редагування страви";
    public bool IsExisting => EditId != 0;

    public string RecipeCostText
    {
        get
        {
            var cost = Recipe.Where(r => r.Ingredient is not null && r.Amount is > 0)
                .Sum(r => r.Amount!.Value * r.Ingredient!.CostPerUnit);
            if (cost == 0) return "Собівартість не розрахована";
            var margin = EditPrice is > 0 ? $" · маржа {(1 - cost / EditPrice.Value) * 100:0} %" : string.Empty;
            return $"Собівартість порції {Format.MoneyExact(cost)}{margin}";
        }
    }

    public override void OnActivated() => Load();

    private void Load(int? selectDishId = null)
    {
        var categories = _menu.GetCategories();
        _ingredients = _inventory.GetIngredients();
        var selectedCategoryId = CategoryItems.FirstOrDefault(c => c.IsSelected)?.Category?.Id;

        CategoryItems.Clear();
        CategoryItems.Add(new MenuCategoryItem(null, categories.Sum(c => c.Dishes.Count), SelectCategory));
        foreach (var c in categories)
            CategoryItems.Add(new MenuCategoryItem(c, c.Dishes.Count, SelectCategory));
        (CategoryItems.FirstOrDefault(c => c.Category?.Id == selectedCategoryId) ?? CategoryItems[0]).IsSelected = true;

        CategoryOptions.Clear();
        foreach (var c in categories)
            CategoryOptions.Add(c);

        _allRows = _menu.GetDishes().Select(d => new MenuDishRow(d, SelectRow, ToggleAvailability)).ToList();
        ApplyFilter();

        var toSelect = _allRows.FirstOrDefault(r => r.Dish.Id == (selectDishId ?? EditId)) ?? Rows.FirstOrDefault();
        if (toSelect is not null) SelectRow(toSelect);
        else NewDish();
    }

    private void SelectCategory(MenuCategoryItem item)
    {
        foreach (var c in CategoryItems)
            c.IsSelected = ReferenceEquals(c, item);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var categoryId = CategoryItems.FirstOrDefault(c => c.IsSelected)?.Category?.Id;
        var term = Search.Trim();
        Rows.Clear();
        foreach (var r in _allRows
                     .Where(r => categoryId is null || r.Dish.CategoryId == categoryId)
                     .Where(r => term.Length == 0 || r.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            Rows.Add(r);
    }

    private void SelectRow(MenuDishRow row)
    {
        foreach (var r in _allRows)
            r.IsSelected = ReferenceEquals(r, row);

        var d = row.Dish;
        EditId = d.Id;
        EditName = d.Name;
        EditDescription = d.Description;
        EditPortion = d.Portion;
        EditPrice = d.Price;
        EditCookTime = d.CookTimeMinutes;
        EditAvailable = d.IsAvailable;
        EditCategory = CategoryOptions.FirstOrDefault(c => c.Id == d.CategoryId);

        Recipe.Clear();
        foreach (var line in d.Ingredients)
            AddRecipeLine(_ingredients.FirstOrDefault(i => i.Id == line.IngredientId), line.Amount);
        OnPropertyChanged(nameof(RecipeCostText));
    }

    private void AddRecipeLine(Ingredient? ingredient, decimal? amount)
    {
        var line = new RecipeLine(_ingredients, RemoveRecipeLine) { Ingredient = ingredient, Amount = amount };
        line.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RecipeCostText));
        Recipe.Add(line);
    }

    private void RemoveRecipeLine(RecipeLine line)
    {
        Recipe.Remove(line);
        OnPropertyChanged(nameof(RecipeCostText));
    }

    partial void OnEditPriceChanged(decimal? value) => OnPropertyChanged(nameof(RecipeCostText));

    [RelayCommand]
    private void AddIngredient() => AddRecipeLine(null, null);

    [RelayCommand]
    private void NewDish()
    {
        foreach (var r in _allRows)
            r.IsSelected = false;
        EditId = 0;
        EditName = string.Empty;
        EditDescription = string.Empty;
        EditPortion = string.Empty;
        EditPrice = null;
        EditCookTime = 10;
        EditAvailable = true;
        EditCategory = CategoryItems.FirstOrDefault(c => c.IsSelected)?.Category is { } selected
            ? CategoryOptions.FirstOrDefault(c => c.Id == selected.Id)
            : CategoryOptions.FirstOrDefault();
        Recipe.Clear();
        OnPropertyChanged(nameof(RecipeCostText));
    }

    [RelayCommand]
    private void Save()
    {
        if (Recipe.Any(r => r.Ingredient is null))
        {
            Toasts.Warning("Оберіть інгредієнт у кожному рядку техкарти");
            return;
        }

        var dish = new Dish
        {
            Id = EditId,
            Name = EditName,
            Description = EditDescription,
            Portion = EditPortion,
            Price = EditPrice ?? 0,
            CookTimeMinutes = (int)(EditCookTime ?? 10),
            IsAvailable = EditAvailable,
            CategoryId = EditCategory?.Id ?? 0
        };
        var recipe = Recipe.Select(r => (r.Ingredient!.Id, r.Amount ?? 0)).ToList();

        Dish? saved = null;
        if (Try(() => saved = _menu.SaveDish(dish, recipe), EditId == 0 ? "Страву додано до меню" : "Зміни збережено", EditName))
            Load(saved!.Id);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (EditId == 0) return;
        if (!await _dialogs.ConfirmAsync("Видалити страву?", $"«{EditName}» буде прибрано з меню.", "Видалити", danger: true))
            return;

        var deleted = false;
        if (!Try(() => deleted = _menu.DeleteDish(EditId)))
            return;

        if (deleted) Toasts.Success("Страву видалено", EditName);
        else Toasts.Info("Страву знято з продажу", "Вона є в історії замовлень, тому зберігається для звітності");
        EditId = 0;
        Load();
    }

    [RelayCommand]
    private void AddCategory()
    {
        var name = NewCategoryName.Trim();
        var palette = new[] { "#7BC67E", "#E07A5F", "#F2A65A", "#F4D35E", "#E58FB5", "#6FB7E8", "#B794F6", "#4ECDC4" };
        var icon = MaterialIconParser.CategoryIcons[CategoryOptions.Count % MaterialIconParser.CategoryIcons.Length];
        var color = palette[CategoryOptions.Count % palette.Length];
        if (Try(() => _menu.SaveCategory(new Category { Name = name, Icon = icon, Color = color }), "Категорію створено", name))
        {
            NewCategoryName = string.Empty;
            Load();
        }
    }

    [RelayCommand]
    private async Task DeleteCategory()
    {
        var item = CategoryItems.FirstOrDefault(c => c.IsSelected);
        if (item?.Category is null)
        {
            Toasts.Info("Оберіть категорію зліва");
            return;
        }
        if (!await _dialogs.ConfirmAsync("Видалити категорію?", $"Категорію «{item.Title}» буде видалено.", "Видалити", danger: true))
            return;
        if (Try(() => _menu.DeleteCategory(item.Category.Id), "Категорію видалено", item.Title))
        {
            CategoryItems[0].IsSelected = true;
            Load();
        }
    }

    private void ToggleAvailability(MenuDishRow row)
    {
        if (Try(() => _menu.SetAvailability(row.Dish.Id, !row.Dish.IsAvailable)))
            Load(EditId);
    }
}
