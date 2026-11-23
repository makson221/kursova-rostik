using Microsoft.Extensions.DependencyInjection;
using RestaurantSystem.ViewModels;

namespace RestaurantSystem.Infrastructure;

/// <summary>Перемикання сторінок у робочій області застосунку.</summary>
public sealed class NavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public PageViewModel? Current { get; private set; }

    public event EventHandler<PageViewModel>? Navigated;

    public T Navigate<T>(Action<T>? initialize = null) where T : PageViewModel
    {
        var page = _services.GetRequiredService<T>();
        initialize?.Invoke(page);

        Current?.OnDeactivated();
        Current = page;
        page.OnActivated();
        Navigated?.Invoke(this, page);
        return page;
    }

    public void Reset()
    {
        Current?.OnDeactivated();
        Current = null;
    }
}
