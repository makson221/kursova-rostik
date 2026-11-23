using CommunityToolkit.Mvvm.ComponentModel;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

/// <summary>Базовий клас усіх ViewModel, що мають власне представлення (View).</summary>
public abstract class ViewModelBase : ObservableObject
{
}

/// <summary>Сторінка робочої області (Огляд, Зала, Кухня...).</summary>
public abstract class PageViewModel : ViewModelBase
{
    protected PageViewModel(ToastService toasts)
    {
        Toasts = toasts;
    }

    protected ToastService Toasts { get; }

    public abstract string Title { get; }

    public virtual string Subtitle => Format.LongDate(DateTime.Now);

    /// <summary>Тип сторінки, пункт меню якої підсвічується (для вкладених сторінок).</summary>
    public virtual Type NavTarget => GetType();

    public virtual void OnActivated()
    {
    }

    public virtual void OnDeactivated()
    {
    }

    /// <summary>Виконує дію, перетворюючи помилки бізнес-логіки на спливаючі повідомлення.</summary>
    protected bool Try(Action action, string? successTitle = null, string successMessage = "")
    {
        try
        {
            action();
            if (successTitle is not null)
                Toasts.Success(successTitle, successMessage);
            return true;
        }
        catch (BusinessException ex)
        {
            Toasts.Warning(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Toasts.Error("Сталася помилка", ex.Message);
            return false;
        }
    }
}
