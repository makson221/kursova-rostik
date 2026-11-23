using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Керування меню: категорії, страви, технологічні карти.</summary>
public sealed class MenuService
{
    private readonly DbContextFactory _db;

    public MenuService(DbContextFactory db)
    {
        _db = db;
    }

    public List<Category> GetCategories()
    {
        using var db = _db.Create();
        return db.Categories.AsNoTracking()
            .Include(c => c.Dishes)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToList();
    }

    public List<Dish> GetDishes()
    {
        using var db = _db.Create();
        return db.Dishes.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Ingredients).ThenInclude(i => i.Ingredient)
            .OrderBy(d => d.Category!.SortOrder).ThenBy(d => d.Name)
            .ToList();
    }

    public Category SaveCategory(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
            throw new BusinessException("Вкажіть назву категорії");

        using var db = _db.Create();
        var name = category.Name.Trim();
        if (db.Categories.Any(c => c.Id != category.Id && c.Name == name))
            throw new BusinessException($"Категорія «{name}» вже існує");

        var entity = category.Id == 0 ? new Category() : db.Categories.Find(category.Id)
            ?? throw new BusinessException("Категорію не знайдено");
        entity.Name = name;
        entity.Icon = category.Icon;
        entity.Color = category.Color;
        entity.SortOrder = category.Id == 0
            ? (db.Categories.Max(c => (int?)c.SortOrder) ?? 0) + 1
            : category.SortOrder;

        if (category.Id == 0)
            db.Categories.Add(entity);
        db.SaveChanges();
        return entity;
    }

    public void DeleteCategory(int id)
    {
        using var db = _db.Create();
        if (db.Dishes.Any(d => d.CategoryId == id))
            throw new BusinessException("Неможливо видалити категорію, у якій є страви");
        var entity = db.Categories.Find(id);
        if (entity is null) return;
        db.Categories.Remove(entity);
        db.SaveChanges();
    }

    public Dish SaveDish(Dish dish, IReadOnlyCollection<(int IngredientId, decimal Amount)> recipe)
    {
        if (string.IsNullOrWhiteSpace(dish.Name))
            throw new BusinessException("Вкажіть назву страви");
        if (dish.Price <= 0)
            throw new BusinessException("Ціна має бути більшою за нуль");
        if (dish.CategoryId == 0)
            throw new BusinessException("Оберіть категорію");
        if (recipe.Any(r => r.Amount <= 0))
            throw new BusinessException("Кількість інгредієнта в рецептурі має бути більшою за нуль");
        if (recipe.GroupBy(r => r.IngredientId).Any(g => g.Count() > 1))
            throw new BusinessException("Інгредієнт повторюється в рецептурі");

        using var db = _db.Create();
        var entity = dish.Id == 0
            ? new Dish()
            : db.Dishes.Include(d => d.Ingredients).FirstOrDefault(d => d.Id == dish.Id)
              ?? throw new BusinessException("Страву не знайдено");

        entity.Name = dish.Name.Trim();
        entity.Description = dish.Description.Trim();
        entity.Price = dish.Price;
        entity.Portion = dish.Portion.Trim();
        entity.CookTimeMinutes = Math.Max(1, dish.CookTimeMinutes);
        entity.IsAvailable = dish.IsAvailable;
        entity.CategoryId = dish.CategoryId;

        db.DishIngredients.RemoveRange(entity.Ingredients);
        entity.Ingredients = recipe
            .Select(r => new DishIngredient { IngredientId = r.IngredientId, Amount = r.Amount })
            .ToList();

        if (dish.Id == 0)
            db.Dishes.Add(entity);
        db.SaveChanges();
        return entity;
    }

    /// <summary>
    /// Видаляє страву. Якщо вона вже фігурує в замовленнях — лише знімає з продажу,
    /// щоб не порушити історію та звітність. Повертає true, якщо страву видалено фізично.
    /// </summary>
    public bool DeleteDish(int id)
    {
        using var db = _db.Create();
        var entity = db.Dishes.Find(id);
        if (entity is null) return true;

        if (db.OrderItems.Any(i => i.DishId == id))
        {
            entity.IsAvailable = false;
            db.SaveChanges();
            return false;
        }

        db.Dishes.Remove(entity);
        db.SaveChanges();
        return true;
    }

    public void SetAvailability(int dishId, bool available)
    {
        using var db = _db.Create();
        var entity = db.Dishes.Find(dishId) ?? throw new BusinessException("Страву не знайдено");
        entity.IsAvailable = available;
        db.SaveChanges();
    }
}
