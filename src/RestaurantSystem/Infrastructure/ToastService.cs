using System.Collections.ObjectModel;
using Avalonia.Threading;
using Material.Icons;

namespace RestaurantSystem.Infrastructure;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error
}

public sealed class ToastMessage
{
    public ToastMessage(ToastKind kind, string title, string message)
    {
        Kind = kind;
        Title = title;
        Message = message;
    }

    public ToastKind Kind { get; }
    public string Title { get; }
    public string Message { get; }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public MaterialIconKind Icon => Kind switch
    {
        ToastKind.Success => MaterialIconKind.CheckCircle,
        ToastKind.Warning => MaterialIconKind.AlertCircle,
        ToastKind.Error => MaterialIconKind.CloseCircle,
        _ => MaterialIconKind.Information
    };

    public bool IsSuccess => Kind == ToastKind.Success;
    public bool IsInfo => Kind == ToastKind.Info;
    public bool IsWarning => Kind == ToastKind.Warning;
    public bool IsError => Kind == ToastKind.Error;
}

/// <summary>Спливаючі повідомлення у правому нижньому куті вікна.</summary>
public sealed class ToastService
{
    private const int MaxVisible = 4;

    public ObservableCollection<ToastMessage> Items { get; } = new();

    public void Success(string title, string message = "") => Show(ToastKind.Success, title, message);
    public void Info(string title, string message = "") => Show(ToastKind.Info, title, message);
    public void Warning(string title, string message = "") => Show(ToastKind.Warning, title, message);
    public void Error(string title, string message = "") => Show(ToastKind.Error, title, message);

    public void Show(ToastKind kind, string title, string message = "")
    {
        var toast = new ToastMessage(kind, title, message);
        Items.Add(toast);
        while (Items.Count > MaxVisible)
            Items.RemoveAt(0);

        var lifetime = kind == ToastKind.Error ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(4);
        DispatcherTimer.RunOnce(() => Items.Remove(toast), lifetime);
    }

    public void Dismiss(ToastMessage toast) => Items.Remove(toast);
}
