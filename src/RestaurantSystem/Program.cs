using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace RestaurantSystem;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var culture = CultureInfo.GetCultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Використовується також дизайнером Avalonia — не видаляти.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = App.UiFontFamily })
            .LogToTrace();
}
