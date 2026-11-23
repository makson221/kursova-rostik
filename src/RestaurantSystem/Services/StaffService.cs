using Microsoft.EntityFrameworkCore;
using RestaurantSystem.Data;
using RestaurantSystem.Models;

namespace RestaurantSystem.Services;

/// <summary>Персонал ресторану: облікові записи, ролі, блокування.</summary>
public sealed class StaffService
{
    private readonly DbContextFactory _db;

    public StaffService(DbContextFactory db)
    {
        _db = db;
    }

    public List<Employee> GetEmployees()
    {
        using var db = _db.Create();
        return db.Employees.AsNoTracking()
            .AsEnumerable()
            .OrderBy(e => e.Role).ThenBy(e => e.FullName)
            .ToList();
    }

    /// <param name="newPassword">Новий пароль або null, щоб залишити поточний.</param>
    public Employee Save(Employee employee, string? newPassword, int currentUserId)
    {
        if (string.IsNullOrWhiteSpace(employee.FullName))
            throw new BusinessException("Вкажіть ПІБ працівника");
        if (string.IsNullOrWhiteSpace(employee.Login) || employee.Login.Trim().Contains(' '))
            throw new BusinessException("Логін не може бути порожнім або містити пробіли");
        if (employee.Id == 0 && string.IsNullOrEmpty(newPassword))
            throw new BusinessException("Задайте пароль для нового працівника");
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 4)
            throw new BusinessException("Пароль має містити щонайменше 4 символи");
        if (employee.Id == currentUserId && !employee.IsActive)
            throw new BusinessException("Неможливо заблокувати власний обліковий запис");

        using var db = _db.Create();
        var login = employee.Login.Trim().ToLower();
        if (db.Employees.Any(e => e.Id != employee.Id && e.Login.ToLower() == login))
            throw new BusinessException($"Логін «{login}» вже зайнятий");

        var entity = employee.Id == 0 ? new Employee() : db.Employees.Find(employee.Id)
            ?? throw new BusinessException("Працівника не знайдено");

        if (entity.Id == currentUserId && entity.Role == UserRole.Administrator && employee.Role != UserRole.Administrator)
            throw new BusinessException("Неможливо зняти роль адміністратора із себе");

        entity.FullName = employee.FullName.Trim();
        entity.Login = login;
        entity.Role = employee.Role;
        entity.Phone = employee.Phone.Trim();
        entity.IsActive = employee.IsActive;
        if (employee.Id == 0)
            entity.HiredAt = DateTime.Today;
        if (!string.IsNullOrEmpty(newPassword))
            entity.PasswordHash = PasswordHasher.Hash(newPassword);

        if (employee.Id == 0)
            db.Employees.Add(entity);
        db.SaveChanges();
        return entity;
    }
}
