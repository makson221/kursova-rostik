using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace RestaurantSystem.Data;

/// <summary>
/// Фабрика контекстів БД. Кожна операція сервісу створює короткоживучий контекст —
/// це рекомендований підхід для десктопних застосунків з EF Core.
/// </summary>
public sealed class DbContextFactory
{
    private readonly DbContextOptions<RestaurantDbContext> _options;

    public DbContextFactory(DbContextOptions<RestaurantDbContext> options)
    {
        _options = options;
    }

    public RestaurantDbContext Create() => new(_options);

    /// <summary>Шлях до файлу БД за замовчуванням: %LocalAppData%/VatraRestaurant/restaurant.db</summary>
    public static string DefaultDatabasePath
    {
        get
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
            if (string.IsNullOrEmpty(root))
                root = AppContext.BaseDirectory; // запасний варіант, якщо системна папка недоступна
            var dir = Path.Combine(root, "VatraRestaurant");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "restaurant.db");
        }
    }

    public static DbContextFactory ForFile(string path) =>
        new(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options);

    /// <summary>БД у пам'яті (для тестів). З'єднання має лишатися відкритим весь час роботи.</summary>
    public static DbContextFactory InMemory(SqliteConnection openConnection) =>
        new(new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(openConnection)
            .Options);
}
