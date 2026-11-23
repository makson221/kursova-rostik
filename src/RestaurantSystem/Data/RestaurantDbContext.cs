using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Models;

namespace RestaurantSystem.Data;

/// <summary>Контекст бази даних ресторану (SQLite через Entity Framework Core).</summary>
public class RestaurantDbContext : DbContext
{
    public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();
    public DbSet<RestaurantTable> Tables => Set<RestaurantTable>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Employee>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Login).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Dish>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Category)
                .WithMany(c => c.Dishes)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Ingredient>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<DishIngredient>(e =>
        {
            e.HasOne(x => x.Dish)
                .WithMany(d => d.Ingredients)
                .HasForeignKey(x => x.DishId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DishId, x.IngredientId }).IsUnique();
        });

        b.Entity<RestaurantTable>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Shape).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Reservation>(e =>
        {
            e.HasOne(x => x.Table)
                .WithMany()
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.StartsAt);
        });

        b.Entity<Order>(e =>
        {
            e.HasOne(x => x.Table)
                .WithMany()
                .HasForeignKey(x => x.TableId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Waiter)
                .WithMany()
                .HasForeignKey(x => x.WaiterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Status);
        });

        b.Entity<OrderItem>(e =>
        {
            e.HasOne(x => x.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Dish)
                .WithMany()
                .HasForeignKey(x => x.DishId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
