using CommunityToolkit.Mvvm.Input;

namespace RestaurantSystem.ViewModels.Dialogs;

public sealed partial class ConfirmDialogViewModel : DialogViewModel<bool>
{
    public ConfirmDialogViewModel(string title, string message, string confirmText, bool isDanger)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        IsDanger = isDanger;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public bool IsDanger { get; }
    public bool IsNormal => !IsDanger;

    [RelayCommand]
    private void Confirm() => Close(true);
}
