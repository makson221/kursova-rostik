using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Автентифікація та поточна сесія користувача.</summary>
public sealed class AuthService
{
    private readonly DbContextFactory _db;

    public AuthService(DbContextFactory db)
    {
        _db = db;
    }

    public Employee? CurrentUser { get; private set; }

    public event EventHandler? CurrentUserChanged;

    public Employee Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            throw new BusinessException("Введіть логін і пароль");

        using var db = _db.Create();
        var normalized = login.Trim().ToLower();
        var user = db.Employees.AsNoTracking().FirstOrDefault(e => e.Login.ToLower() == normalized);

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
            throw new BusinessException("Невірний логін або пароль");
        if (!user.IsActive)
            throw new BusinessException("Обліковий запис заблоковано. Зверніться до адміністратора");

        CurrentUser = user;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        return user;
    }

    public void Logout()
    {
        CurrentUser = null;
        CurrentUserChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsInRole(params UserRole[] roles) =>
        CurrentUser is not null && roles.Contains(CurrentUser.Role);

    public Employee RequireUser() =>
        CurrentUser ?? throw new BusinessException("Сесію завершено. Увійдіть знову");
}
