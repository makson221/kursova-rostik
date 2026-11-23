using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSystem.Data;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Services;
using RestaurantSystem.ViewModels;
using RestaurantSystem.Views;

namespace RestaurantSystem;

public partial class App : Application
{
    public const string UiFontFamily = "avares://RestaurantSystem/Assets/Fonts#Manrope";

    /// <summary>Контейнер залежностей застосунку.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices(DbContextFactory.ForFile(DbContextFactory.DefaultDatabasePath));
        DbSeeder.EnsureCreatedAndSeeded(Services.GetRequiredService<DbContextFactory>());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static IServiceProvider ConfigureServices(DbContextFactory dbFactory)
    {
        var services = new ServiceCollection();

        // Дані та бізнес-логіка
        services.AddSingleton(dbFactory);
        services.AddSingleton<AuthService>();
        services.AddSingleton<MenuService>();
        services.AddSingleton<OrderService>();
        services.AddSingleton<HallService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<ReservationService>();
        services.AddSingleton<StaffService>();
        services.AddSingleton<ReportService>();

        // Інфраструктура UI
        services.AddSingleton<ToastService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<NavigationService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<HallViewModel>();
        services.AddTransient<OrderViewModel>();
        services.AddTransient<KitchenViewModel>();
        services.AddTransient<MenuViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<ReservationsViewModel>();
        services.AddTransient<StaffViewModel>();
        services.AddTransient<ReportsViewModel>();

        var provider = services.BuildServiceProvider();
        Services = provider;
        return provider;
    }
}
