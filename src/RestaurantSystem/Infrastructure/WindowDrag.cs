using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace RestaurantSystem.Infrastructure;

/// <summary>
/// Приєднана властивість: робить елемент «ручкою» для перетягування вікна без системного заголовка.
/// Подвійний клік розгортає/відновлює вікно.
/// </summary>
public static class WindowDrag
{
    public static readonly AttachedProperty<bool> IsDragAreaProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsDragArea", typeof(WindowDrag));

    static WindowDrag()
    {
        IsDragAreaProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is true)
            {
                control.PointerPressed += OnPointerPressed;
                control.DoubleTapped += OnDoubleTapped;
            }
            else
            {
                control.PointerPressed -= OnPointerPressed;
                control.DoubleTapped -= OnDoubleTapped;
            }
        });
    }

    public static bool GetIsDragArea(Control control) => control.GetValue(IsDragAreaProperty);
    public static void SetIsDragArea(Control control, bool value) => control.SetValue(IsDragAreaProperty, value);

    private static bool IsInteractive(object? source) =>
        source is Visual v && (v.FindAncestorOfType<Button>(true) is not null ||
                               v.FindAncestorOfType<TextBox>(true) is not null ||
                               v.FindAncestorOfType<SelectingItemsControl>(true) is not null);

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || IsInteractive(e.Source))
            return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed || e.ClickCount > 1)
            return;
        if (TopLevel.GetTopLevel(control) is Window window)
            window.BeginMoveDrag(e);
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || IsInteractive(e.Source))
            return;
        if (TopLevel.GetTopLevel(control) is Window window)
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }
}
