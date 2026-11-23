using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed class EmployeeCard : ObservableObject
{
    public EmployeeCard(Employee employee, WaiterStat? stats, Action<EmployeeCard> select)
    {
        Employee = employee;
        Stats = stats;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public Employee Employee { get; }
    public WaiterStat? Stats { get; }
    public string FullName => Employee.FullName;
    public string Initials => Employee.Initials;
    public string RoleText => Employee.Role.ToText();
    public string Login => "@" + Employee.Login;
    public string Phone => string.IsNullOrWhiteSpace(Employee.Phone) ? "—" : Employee.Phone;
    public bool IsInactive => !Employee.IsActive;
    public double CardOpacity => Employee.IsActive ? 1 : 0.5;
    public bool HasStats => Stats is not null;
    public string StatsText => Stats is null ? string.Empty
        : $"{Format.Plural(Stats.Orders, "чек", "чеки", "чеків")} · {Format.Money(Stats.Revenue)}";

    public string TenureText
    {
        get
        {
            var days = (DateTime.Today - Employee.HiredAt.Date).Days;
            if (days < 30) return "у команді з " + Employee.HiredAt.ToString("dd.MM.yyyy");
            var months = days / 30;
            return months < 12
                ? "у команді " + Format.Plural(months, "місяць", "місяці", "місяців")
                : "у команді " + Format.Plural(months / 12, "рік", "роки", "років") +
                  (months % 12 > 0 ? " " + Format.Plural(months % 12, "міс.", "міс.", "міс.") : string.Empty);
        }
    }

    public IBrush AvatarBrush => Employee.Role switch
    {
        UserRole.Administrator => Tones.Gradient(Color.Parse("#FFC977"), Color.Parse("#E4483A")),
        UserRole.Waiter => Tones.Gradient(Color.Parse("#9CC8FF"), Color.Parse("#4C7FD6")),
        _ => Tones.Gradient(Color.Parse("#A6EBC2"), Color.Parse("#3FA56C"))
    };

    public IBrush RoleTone => Employee.Role switch
    {
        UserRole.Administrator => Tones.Accent,
        UserRole.Waiter => Tones.Blue,
        _ => Tones.Green
    };

    public IBrush RoleSoft => Tones.Soft(Employee.Role switch
    {
        UserRole.Administrator => Tones.AccentColor,
        UserRole.Waiter => Tones.BlueColor,
        _ => Tones.GreenColor
    });

    public IRelayCommand SelectCommand { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record RoleOption(UserRole Role, string Title)
{
    public override string ToString() => Title;
}

/// <summary>Персонал: картки працівників і форма облікового запису.</summary>
public sealed partial class StaffViewModel : PageViewModel
{
    private readonly StaffService _staff;
    private readonly ReportService _reports;
    private readonly AuthService _auth;
    private List<EmployeeCard> _cards = new();

    public StaffViewModel(ToastService toasts, StaffService staff, ReportService reports, AuthService auth) : base(toasts)
    {
        _staff = staff;
        _reports = reports;
        _auth = auth;
        Roles = Enum.GetValues<UserRole>().Select(r => new RoleOption(r, r.ToText())).ToList();
    }

    public override string Title => "Персонал";
    public override string Subtitle => "Облікові записи, ролі та результати за поточний місяць";

    public ObservableCollection<EmployeeCard> Cards { get; } = new();
    public IReadOnlyList<RoleOption> Roles { get; }

    public int ActiveCount => _cards.Count(c => c.Employee.IsActive);
    public int WaitersCount => _cards.Count(c => c.Employee.Role == UserRole.Waiter && c.Employee.IsActive);
    public int CooksCount => _cards.Count(c => c.Employee.Role == UserRole.Cook && c.Employee.IsActive);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(IsExisting), nameof(PasswordHint))]
    private int _editId;

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _login = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private RoleOption? _role;

    public string FormTitle => EditId == 0 ? "Новий працівник" : FullName;
    public bool IsExisting => EditId != 0;
    public string PasswordHint => EditId == 0 ? "Пароль" : "Новий пароль (залиште порожнім, щоб не змінювати)";

    public override void OnActivated() => Load();

    private void Load(int? selectId = null)
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var stats = _reports.Build(monthStart, DateTime.Today.AddDays(1)).Waiters;

        _cards = _staff.GetEmployees()
            .Select(e => new EmployeeCard(e, stats.FirstOrDefault(s => s.Name == e.FullName), Select))
            .ToList();
        Cards.Clear();
        foreach (var c in _cards)
            Cards.Add(c);

        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(WaitersCount));
        OnPropertyChanged(nameof(CooksCount));

        var card = _cards.FirstOrDefault(c => c.Employee.Id == (selectId ?? EditId)) ?? _cards.FirstOrDefault();
        if (card is not null) Select(card);
        else New();
    }

    private void Select(EmployeeCard card)
    {
        foreach (var c in _cards)
            c.IsSelected = ReferenceEquals(c, card);
        var e = card.Employee;
        EditId = e.Id;
        FullName = e.FullName;
        Login = e.Login;
        Phone = e.Phone;
        Password = string.Empty;
        IsActive = e.IsActive;
        Role = Roles.First(r => r.Role == e.Role);
        OnPropertyChanged(nameof(FormTitle));
    }

    [RelayCommand]
    private void New()
    {
        foreach (var c in _cards)
            c.IsSelected = false;
        EditId = 0;
        FullName = string.Empty;
        Login = string.Empty;
        Phone = "+380 ";
        Password = string.Empty;
        IsActive = true;
        Role = Roles.First(r => r.Role == UserRole.Waiter);
    }

    [RelayCommand]
    private void Save()
    {
        var employee = new Employee
        {
            Id = EditId,
            FullName = FullName,
            Login = Login,
            Phone = Phone,
            IsActive = IsActive,
            Role = Role?.Role ?? UserRole.Waiter
        };
        Employee? saved = null;
        if (Try(() => saved = _staff.Save(employee, string.IsNullOrEmpty(Password) ? null : Password, _auth.RequireUser().Id),
                EditId == 0 ? "Працівника додано" : "Дані оновлено", employee.FullName))
            Load(saved!.Id);
    }
}
