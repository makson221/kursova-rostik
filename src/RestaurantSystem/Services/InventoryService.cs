using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Облік складу: залишки інгредієнтів, поповнення, списання за рецептурами.</summary>
public sealed class InventoryService
{
    private readonly DbContextFactory _db;

    public InventoryService(DbContextFactory db)
    {
        _db = db;
    }

    public List<Ingredient> GetIngredients()
    {
        using var db = _db.Create();
        return db.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToList();
    }

    public Ingredient Save(Ingredient ingredient)
    {
        if (string.IsNullOrWhiteSpace(ingredient.Name))
            throw new BusinessException("Вкажіть назву інгредієнта");
        if (string.IsNullOrWhiteSpace(ingredient.Unit))
            throw new BusinessException("Вкажіть одиницю виміру");
        if (ingredient.Quantity < 0 || ingredient.MinQuantity < 0 || ingredient.CostPerUnit < 0)
            throw new BusinessException("Кількість і ціна не можуть бути від'ємними");

        using var db = _db.Create();
        var name = ingredient.Name.Trim();
        if (db.Ingredients.Any(i => i.Id != ingredient.Id && i.Name == name))
            throw new BusinessException($"Інгредієнт «{name}» вже існує");

        var entity = ingredient.Id == 0 ? new Ingredient() : db.Ingredients.Find(ingredient.Id)
            ?? throw new BusinessException("Інгредієнт не знайдено");
        entity.Name = name;
        entity.Unit = ingredient.Unit.Trim();
        entity.Quantity = ingredient.Quantity;
        entity.MinQuantity = ingredient.MinQuantity;
        entity.CostPerUnit = ingredient.CostPerUnit;
        if (ingredient.Id == 0)
            db.Ingredients.Add(entity);
        db.SaveChanges();
        return entity;
    }

    public void Restock(int ingredientId, decimal amount)
    {
        if (amount <= 0)
            throw new BusinessException("Кількість поповнення має бути більшою за нуль");
        using var db = _db.Create();
        var entity = db.Ingredients.Find(ingredientId) ?? throw new BusinessException("Інгредієнт не знайдено");
        entity.Quantity += amount;
        db.SaveChanges();
    }

    public void Delete(int ingredientId)
    {
        using var db = _db.Create();
        if (db.DishIngredients.Any(d => d.IngredientId == ingredientId))
            throw new BusinessException("Інгредієнт використовується в технологічних картах страв");
        var entity = db.Ingredients.Find(ingredientId);
        if (entity is null) return;
        db.Ingredients.Remove(entity);
        db.SaveChanges();
    }

    /// <summary>
    /// Списує інгредієнти для переданих позицій у межах відкритого контексту.
    /// Повертає назви інгредієнтів, залишок яких став нижчим за мінімальний.
    /// </summary>
    internal static List<string> WriteOff(RestaurantDbContext db, IEnumerable<OrderItem> items)
    {
        var warnings = new List<string>();
        foreach (var item in items)
        {
            var recipe = db.DishIngredients
                .Include(r => r.Ingredient)
                .Where(r => r.DishId == item.DishId)
                .ToList();

            foreach (var line in recipe)
            {
                var ingredient = line.Ingredient!;
                var wasLow = ingredient.IsLow;
                ingredient.Quantity = Math.Max(0, ingredient.Quantity - line.Amount * item.Quantity);
                if (!wasLow && ingredient.IsLow)
                    warnings.Add(ingredient.Name);
            }
        }
        return warnings.Distinct().ToList();
    }
}
