namespace RestaurantSystem.Models;

/// <summary>Категорія меню (Салати, Супи, Напої...).</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Назва іконки Material Design (MaterialIconKind).</summary>
    public string Icon { get; set; } = "SilverwareForkKnife";
    /// <summary>Колір категорії у форматі #RRGGBB.</summary>
    public string Color { get; set; } = "#F2A65A";
    public int SortOrder { get; set; }

    public List<Dish> Dishes { get; set; } = new();
}

/// <summary>Страва або напій з меню.</summary>
public class Dish
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    /// <summary>Вихід страви, напр. «250 г» або «300 мл».</summary>
    public string Portion { get; set; } = string.Empty;
    public int CookTimeMinutes { get; set; } = 10;
    public bool IsAvailable { get; set; } = true;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Технологічна карта — склад страви.</summary>
    public List<DishIngredient> Ingredients { get; set; } = new();
}

/// <summary>Інгредієнт на складі.</summary>
public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Одиниця виміру: кг, л, шт.</summary>
    public string Unit { get; set; } = "кг";
    public decimal Quantity { get; set; }
    /// <summary>Мінімальний залишок, нижче якого система попереджає.</summary>
    public decimal MinQuantity { get; set; }
    /// <summary>Закупівельна ціна за одиницю, ₴.</summary>
    public decimal CostPerUnit { get; set; }

    public bool IsLow => Quantity < MinQuantity;
    public bool IsCritical => Quantity < MinQuantity / 2;
}

/// <summary>Рядок технологічної карти: скільки інгредієнта йде на одну порцію.</summary>
public class DishIngredient
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    /// <summary>Кількість інгредієнта (в його одиницях) на одну порцію.</summary>
    public decimal Amount { get; set; }
}
