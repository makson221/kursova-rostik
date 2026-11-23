namespace RestaurantSystem.Models;

/// <summary>Працівник ресторану (обліковий запис користувача системи).</summary>
public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    /// <summary>Хеш пароля у форматі PBKDF2 (ітерації.сіль.хеш).</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime HiredAt { get; set; } = DateTime.Today;

    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => (parts[0][..1] + parts[1][..1]).ToUpperInvariant()
            };
        }
    }

    public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
}
