using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RestaurantSystem.ViewModels;

namespace RestaurantSystem;

/// <summary>
/// Зіставляє ViewModel із View за назвою: XxxViewModel → XxxView.
/// Дозволяє показувати сторінки через ContentControl без явних DataTemplate.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Type?> Cache = new();

    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "—" };

        var vmType = data.GetType();
        if (!Cache.TryGetValue(vmType, out var viewType))
        {
            var name = vmType.FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "View");
            viewType = vmType.Assembly.GetType(name);
            Cache[vmType] = viewType;
        }

        return viewType is not null
            ? (Control)Activator.CreateInstance(viewType)!
            : new TextBlock { Text = "View не знайдено: " + vmType.Name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
