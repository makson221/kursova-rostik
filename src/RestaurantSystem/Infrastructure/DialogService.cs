using CommunityToolkit.Mvvm.ComponentModel;
using RestaurantSystem.ViewModels;
using RestaurantSystem.ViewModels.Dialogs;

namespace RestaurantSystem.Infrastructure;

/// <summary>
/// Модальні діалоги, що малюються всередині вікна (поверх затемнення),
/// а не як окремі системні вікна.
/// </summary>
public sealed partial class DialogService : ObservableObject
{
    [ObservableProperty]
    private DialogViewModelBase? _current;

    public Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        dialog.Closed += OnClosed;
        Current = dialog;
        return dialog.Result;

        void OnClosed(object? sender, EventArgs e)
        {
            dialog.Closed -= OnClosed;
            if (ReferenceEquals(Current, dialog))
                Current = null;
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Підтвердити", bool danger = false)
    {
        var result = await ShowAsync(new ConfirmDialogViewModel(title, message, confirmText, danger));
        return result;
    }
}
