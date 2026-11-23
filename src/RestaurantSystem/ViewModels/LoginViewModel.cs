using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed class DemoAccount
{
    public DemoAccount(string role, string login, string password, MaterialIconKind icon, Action<DemoAccount> fill)
    {
        Role = role;
        Login = login;
        Password = password;
        Icon = icon;
        FillCommand = new RelayCommand(() => fill(this));
    }

    public string Role { get; }
    public string Login { get; }
    public string Password { get; }
    public MaterialIconKind Icon { get; }
    public string Hint => $"{Login} / {Password}";
    public IRelayCommand FillCommand { get; }
}

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly ToastService _toasts;

    public LoginViewModel(AuthService auth, ToastService toasts)
    {
        _auth = auth;
        _toasts = toasts;
        DemoAccounts = new[]
        {
            new DemoAccount("Адміністратор", "admin", "admin", MaterialIconKind.ShieldCrown, Fill),
            new DemoAccount("Офіціант", "waiter", "1111", MaterialIconKind.RoomService, Fill),
            new DemoAccount("Кухар", "cook", "2222", MaterialIconKind.ChefHat, Fill),
        };
    }

    public IReadOnlyList<DemoAccount> DemoAccounts { get; }

    public string Greeting
    {
        get
        {
            var h = DateTime.Now.Hour;
            return h switch
            {
                < 5 => "Доброї ночі",
                < 12 => "Доброго ранку",
                < 18 => "Доброго дня",
                _ => "Доброго вечора"
            };
        }
    }

    public string TodayText => Format.LongDate(DateTime.Now);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = string.Empty;

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(Error);

    partial void OnLoginChanged(string value) => Error = string.Empty;
    partial void OnPasswordChanged(string value) => Error = string.Empty;

    private void Fill(DemoAccount account)
    {
        Login = account.Login;
        Password = account.Password;
    }

    [RelayCommand]
    private void SignIn()
    {
        try
        {
            var user = _auth.Login(Login, Password);
            _toasts.Success("Гарної зміни!", $"{user.FullName} · {user.Role.ToText()}");
        }
        catch (BusinessException ex)
        {
            Error = ex.Message;
        }
    }
}
