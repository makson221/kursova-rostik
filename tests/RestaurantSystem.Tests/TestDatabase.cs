using Microsoft.Data.Sqlite;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Tests;

/// <summary>
/// Ізольована БД SQLite у пам'яті з базовим наповненням
/// (персонал, меню з техкартами, склад, столи) — без історії замовлень.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Factory = DbContextFactory.InMemory(_connection);
        DbSeeder.EnsureCreatedAndSeeded(Factory, withDemoHistory: false);
    }

    public DbContextFactory Factory { get; }

    public Employee User(string login)
    {
        using var db = Factory.Create();
        return db.Employees.Single(e => e.Login == login);
    }

    public RestaurantTable Table(int number)
    {
        using var db = Factory.Create();
        return db.Tables.Single(t => t.Number == number);
    }

    public Dish Dish(string name)
    {
        using var db = Factory.Create();
        return db.Dishes.Single(d => d.Name == name);
    }

    public Ingredient Ingredient(string name)
    {
        using var db = Factory.Create();
        return db.Ingredients.Single(i => i.Name == name);
    }

    public void Dispose() => _connection.Dispose();
}
