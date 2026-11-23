using Material.Icons;

namespace RestaurantSystem.Infrastructure;

public static class MaterialIconParser
{
    public static MaterialIconKind Parse(string? name, MaterialIconKind fallback = MaterialIconKind.SilverwareForkKnife) =>
        Enum.TryParse<MaterialIconKind>(name, out var kind) ? kind : fallback;

    /// <summary>Набір іконок, доступних для категорій меню.</summary>
    public static readonly string[] CategoryIcons =
    {
        "Leaf", "PotSteam", "FoodDrumstick", "Pizza", "Cupcake", "GlassCocktail",
        "Coffee", "Fish", "Noodles", "IceCream", "Hamburger", "Baguette", "GlassWine", "FoodSteak"
    };
}
