using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;

namespace RestaurantSystem.ViewModels.Dialogs;

public sealed record PaymentResult(PaymentMethod Method, decimal DiscountPercent);

public sealed partial class DiscountOption : ObservableObject
{
    public DiscountOption(decimal percent, Action<DiscountOption> select)
    {
        Percent = percent;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public decimal Percent { get; }
    public string Text => Percent == 0 ? "Без знижки" : $"−{Percent:0} %";
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Розрахунок гостя: знижка, спосіб оплати, решта.</summary>
public sealed partial class PaymentDialogViewModel : DialogViewModel<PaymentResult>
{
    private readonly decimal _subtotal;

    public PaymentDialogViewModel(Order order)
    {
        _subtotal = order.Subtotal;
        TableText = $"Стіл №{order.Table?.Number} · {Format.Plural(order.GuestsCount, "гість", "гості", "гостей")}";
        PositionsText = Format.Plural(order.Items.Sum(i => i.Quantity), "позиція", "позиції", "позицій");
        DiscountOptions = new[] { 0m, 5m, 10m, 15m, 20m }
            .Select(p => new DiscountOption(p, SelectDiscount))
            .ToList();
        SelectDiscount(DiscountOptions[0]);
    }

    public string TableText { get; }
    public string PositionsText { get; }
    public string SubtotalText => Format.MoneyExact(_subtotal);
    public IReadOnlyList<DiscountOption> DiscountOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountText), nameof(TotalText), nameof(ChangeText), nameof(HasDiscount), nameof(CanPay))]
    private decimal _discountPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCard), nameof(ChangeText), nameof(CanPay))]
    private bool _isCash = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeText), nameof(CanPay))]
    private decimal? _received;

    public bool IsCard => !IsCash;
    public decimal Total => _subtotal - Math.Round(_subtotal * DiscountPercent / 100m, 2);
    public bool HasDiscount => DiscountPercent > 0;
    public string DiscountText => "−" + Format.MoneyExact(_subtotal - Total);
    public string TotalText => Format.MoneyExact(Total);

    public string ChangeText
    {
        get
        {
            if (!IsCash || Received is null) return "—";
            var change = Received.Value - Total;
            return change >= 0 ? Format.MoneyExact(change) : "Недостатньо коштів";
        }
    }

    public bool CanPay => IsCard || Received is null || Received >= Total;

    private void SelectDiscount(DiscountOption option)
    {
        foreach (var o in DiscountOptions)
            o.IsSelected = ReferenceEquals(o, option);
        DiscountPercent = option.Percent;
    }

    [RelayCommand]
    private void UseCash() => IsCash = true;

    [RelayCommand]
    private void UseCard() => IsCash = false;

    [RelayCommand]
    private void ExactAmount() => Received = Total;

    [RelayCommand]
    private void Confirm()
    {
        if (!CanPay) return;
        Close(new PaymentResult(IsCash ? PaymentMethod.Cash : PaymentMethod.Card, DiscountPercent));
    }
}
