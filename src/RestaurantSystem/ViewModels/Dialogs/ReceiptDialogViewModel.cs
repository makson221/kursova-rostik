using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels.Dialogs;

/// <summary>Перегляд надрукованого чеку зі збереженням у текстовий файл.</summary>
public sealed partial class ReceiptDialogViewModel : DialogViewModel<bool>
{
    private readonly ToastService _toasts;
    private readonly Order _order;

    public ReceiptDialogViewModel(Order order, ToastService toasts)
    {
        _order = order;
        _toasts = toasts;
        ReceiptText = ReceiptPrinter.Build(order);
        TotalText = Format.MoneyExact(order.Total);
    }

    public string ReceiptText { get; }
    public string TotalText { get; }

    public static string ReceiptsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ватра", "Чеки");

    [RelayCommand]
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(ReceiptsDirectory);
            var path = Path.Combine(ReceiptsDirectory, $"Чек_{_order.Id:D6}.txt");
            File.WriteAllText(path, ReceiptText);
            _toasts.Success("Чек збережено", path);
        }
        catch (Exception ex)
        {
            _toasts.Error("Не вдалося зберегти чек", ex.Message);
        }
    }

    [RelayCommand]
    private void Done() => Close(true);
}
