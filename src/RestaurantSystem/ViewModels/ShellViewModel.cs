using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

/// <summary>Пункт бічного меню.</summary>
public sealed partial class NavItem : ObservableObject
{
    public NavItem(string title, MaterialIconKind icon, Type target, Action<NavItem> open)
    {
        Title = title;
        Icon = icon;
        Target = target;
        OpenCommand = new RelayCommand(() => open(this));
    }

    public string Title { get; }
    public MaterialIconKind Icon { get; }
    public Type Target { get; }
    public IRelayCommand OpenCommand { get; }

    [ObservableProperty]
    private bool _isActive;
}

/// <summary>Робоча оболонка: бічне меню, заголовок, поточна сторінка.</summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly AuthService _auth;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
    private readonly DispatcherTimer _clock;

    public ShellViewModel(AuthService auth, NavigationService navigation, DialogService dialogs)
    {
        _auth = auth;
        _navigation = navigation;
        _dialogs = dialogs;

        var user = auth.RequireUser();
        UserName = user.FullName;
        RoleText = user.Role.ToText();
        UserInitials = user.Initials;

        foreach (var item in BuildMenu(user.Role))
            NavItems.Add(item);

        _navigation.Navigated += OnNavigated;

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clock.Tick += (_, _) => OnPropertyChanged(nameof(ClockText));
        _clock.Start();

        NavItems[0].OpenCommand.Execute(null);
    }

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public string UserName { get; }
    public string RoleText { get; }
    public string UserInitials { get; }
    public string ClockText => DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private PageViewModel? _currentPage;

    private IEnumerable<NavItem> BuildMenu(Models.UserRole role)
    {
        NavItem Item(string title, MaterialIconKind icon, Type target) => new(title, icon, target, Open);

        if (role == Models.UserRole.Administrator)
            yield return Item("Огляд", MaterialIconKind.ViewDashboardOutline, typeof(DashboardViewModel));
        if (role is Models.UserRole.Administrator or Models.UserRole.Waiter)
            yield return Item("Зала", MaterialIconKind.TableFurniture, typeof(HallViewModel));
        yield return Item("Кухня", MaterialIconKind.ChefHat, typeof(KitchenViewModel));
        if (role is Models.UserRole.Administrator or Models.UserRole.Waiter)
            yield return Item("Бронювання", MaterialIconKind.CalendarClockOutline, typeof(ReservationsViewModel));
        if (role == Models.UserRole.Administrator)
            yield return Item("Меню", MaterialIconKind.BookOpenPageVariantOutline, typeof(MenuViewModel));
        if (role is Models.UserRole.Administrator or Models.UserRole.Cook)
            yield return Item("Склад", MaterialIconKind.PackageVariantClosed, typeof(InventoryViewModel));
        if (role == Models.UserRole.Administrator)
        {
            yield return Item("Персонал", MaterialIconKind.AccountGroupOutline, typeof(StaffViewModel));
            yield return Item("Звіти", MaterialIconKind.ChartBoxOutline, typeof(ReportsViewModel));
        }
    }

    private void Open(NavItem item)
    {
        if (item.Target == typeof(DashboardViewModel)) _navigation.Navigate<DashboardViewModel>();
        else if (item.Target == typeof(HallViewModel)) _navigation.Navigate<HallViewModel>();
        else if (item.Target == typeof(KitchenViewModel)) _navigation.Navigate<KitchenViewModel>();
        else if (item.Target == typeof(ReservationsViewModel)) _navigation.Navigate<ReservationsViewModel>();
        else if (item.Target == typeof(MenuViewModel)) _navigation.Navigate<MenuViewModel>();
        else if (item.Target == typeof(InventoryViewModel)) _navigation.Navigate<InventoryViewModel>();
        else if (item.Target == typeof(StaffViewModel)) _navigation.Navigate<StaffViewModel>();
        else if (item.Target == typeof(ReportsViewModel)) _navigation.Navigate<ReportsViewModel>();
    }

    private void OnNavigated(object? sender, PageViewModel page)
    {
        CurrentPage = page;
        foreach (var item in NavItems)
            item.IsActive = item.Target == page.NavTarget;
    }

    [RelayCommand]
    private async Task Logout()
    {
        if (await _dialogs.ConfirmAsync("Завершити зміну?", $"{UserName}, ви дійсно бажаєте вийти із системи?", "Вийти"))
            _auth.Logout();
    }

    public void Dispose()
    {
        _clock.Stop();
        _navigation.Navigated -= OnNavigated;
        _navigation.Reset();
    }
}
