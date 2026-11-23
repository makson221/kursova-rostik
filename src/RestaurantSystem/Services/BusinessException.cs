namespace RestaurantSystem.Services;

/// <summary>Порушення бізнес-правила. Повідомлення показується користувачу як є.</summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message)
    {
    }
}
