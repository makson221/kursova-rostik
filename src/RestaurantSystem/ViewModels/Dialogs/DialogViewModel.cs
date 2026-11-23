using CommunityToolkit.Mvvm.Input;

namespace RestaurantSystem.ViewModels.Dialogs;

public abstract class DialogViewModelBase : ViewModelBase
{
    public event EventHandler? Closed;

    protected void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    public abstract IRelayCommand CancelCommand { get; }
}

/// <summary>Діалог, що повертає результат типу <typeparamref name="TResult"/>.</summary>
public abstract class DialogViewModel<TResult> : DialogViewModelBase
{
    private readonly TaskCompletionSource<TResult?> _tcs = new();

    protected DialogViewModel()
    {
        CancelCommand = new RelayCommand(() => Close(default));
    }

    public Task<TResult?> Result => _tcs.Task;

    public override IRelayCommand CancelCommand { get; }

    protected void Close(TResult? result)
    {
        if (_tcs.TrySetResult(result))
            RaiseClosed();
    }
}
