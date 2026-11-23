using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

/// <summary>
/// Корінь застосунку: показує екран входу або робочу оболонку
/// залежно від того, чи увійшов користувач.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly AuthService _auth;

    public MainWindowViewModel(IServiceProvider services, AuthService auth, DialogService dialogs, ToastService toasts)
    {
        _services = services;
        _auth = auth;
        Dialogs = dialogs;
        Toasts = toasts;

        _auth.CurrentUserChanged += (_, _) => UpdateRoot();
        UpdateRoot();
    }

    public DialogService Dialogs { get; }
    public ToastService Toasts { get; }

    [ObservableProperty]
    private ViewModelBase? _root;

    private void UpdateRoot()
    {
        if (Root is ShellViewModel oldShell)
            oldShell.Dispose();

        Root = _auth.CurrentUser is null
            ? _services.GetRequiredService<LoginViewModel>()
            : _services.GetRequiredService<ShellViewModel>();
    }
}
