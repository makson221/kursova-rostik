using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RestaurantSystem.Controls;
using RestaurantSystem.Infrastructure;
using RestaurantSystem.Services;

namespace RestaurantSystem.ViewModels;

public sealed partial class PeriodChip : ObservableObject
{
    public PeriodChip(string title, Func<DateTime, (DateTime From, DateTime To)> range, Action<PeriodChip> select)
    {
        Title = title;
        Range = range;
        SelectCommand = new RelayCommand(() => select(this));
    }

    public string Title { get; }
    public Func<DateTime, (DateTime From, DateTime To)> Range { get; }
    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed record LegendRow(string Title, string Value, string Share, IBrush Tone);

public sealed record DishReportRow(int Rank, string Name, string Category, string Quantity, string Revenue, double Ratio);

public sealed record WaiterReportRow(string Initials, string Name, string Orders, string Revenue, string AverageCheck, double Ratio);

/// <summary>Аналітика продажів за обраний період.</summary>
public sealed partial class ReportsViewModel : PageViewModel
{
    private readonly ReportService _reports;
    private SalesReport? _report;

    public ReportsViewModel(ToastService toasts, ReportService reports) : base(toasts)
    {
        _reports = reports;
        Periods = new[]
        {
            new PeriodChip("Сьогодні", n => (n.Date, n.Date.AddDays(1)), SelectPeriod),
            new PeriodChip("7 днів", n => (n.Date.AddDays(-6), n.Date.AddDays(1)), SelectPeriod),
            new PeriodChip("30 днів", n => (n.Date.AddDays(-29), n.Date.AddDays(1)), SelectPeriod),
            new PeriodChip("Цей місяць", n => (new DateTime(n.Year, n.Month, 1), n.Date.AddDays(1)), SelectPeriod),
        };
        Periods[2].IsSelected = true;
    }

    public override string Title => "Звіти та аналітика";
    public override string Subtitle => _report is null ? string.Empty
        : $"Період: {_report.From:dd.MM.yyyy} – {_report.To.AddDays(-1):dd.MM.yyyy}";

    public IReadOnlyList<PeriodChip> Periods { get; }

    public IReadOnlyList<KpiCard> Kpis { get; private set; } = Array.Empty<KpiCard>();
    public IReadOnlyList<ChartPoint> DailyChart { get; private set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<ChartPoint> HourlyChart { get; private set; } = Array.Empty<ChartPoint>();
    public IReadOnlyList<DonutSegment> CategorySegments { get; private set; } = Array.Empty<DonutSegment>();
    public IReadOnlyList<LegendRow> CategoryLegend { get; private set; } = Array.Empty<LegendRow>();
    public IReadOnlyList<DishReportRow> TopDishes { get; private set; } = Array.Empty<DishReportRow>();
    public IReadOnlyList<WaiterReportRow> Waiters { get; private set; } = Array.Empty<WaiterReportRow>();

    public string RevenueText => Format.Money(_report?.Revenue ?? 0);
    public string CashText => Format.Money(_report?.CashRevenue ?? 0);
    public string CardText => Format.Money(_report?.CardRevenue ?? 0);
    public double CashShare => _report is { Revenue: > 0 } r ? (double)(r.CashRevenue / r.Revenue) : 0;
    public double CardShare => _report is { Revenue: > 0 } r ? (double)(r.CardRevenue / r.Revenue) : 0;
    public string CashShareText => $"{CashShare * 100:0} %";
    public string CardShareText => $"{CardShare * 100:0} %";
    public string DiscountsText => Format.Money(_report?.DiscountsTotal ?? 0);
    public bool IsEmpty => _report is null || _report.OrdersCount == 0;
    public bool HasData => !IsEmpty;

    public override void OnActivated() => Build();

    private void SelectPeriod(PeriodChip chip)
    {
        foreach (var p in Periods)
            p.IsSelected = ReferenceEquals(p, chip);
        Build();
    }

    private void Build()
    {
        var (from, to) = Periods.First(p => p.IsSelected).Range(DateTime.Now);
        var r = _report = _reports.Build(from, to);
        var days = Math.Max(1, (to - from).Days);
        var prev = _reports.Build(from.AddDays(-days), from);

        var margin = r.Revenue > 0 ? (1 - r.FoodCost / r.Revenue) * 100 : 0;
        Kpis = new[]
        {
            new KpiCard("Виручка", Format.Money(r.Revenue), Trend(r.Revenue, prev.Revenue),
                MaterialIconKind.CashMultiple, Tones.Accent, Tones.Soft(Tones.AccentColor),
                prev.Revenue == 0 ? null : r.Revenue >= prev.Revenue),
            new KpiCard("Чеків", r.OrdersCount.ToString("N0", Format.Uk), Trend(r.OrdersCount, prev.OrdersCount),
                MaterialIconKind.ReceiptTextOutline, Tones.Green, Tones.Soft(Tones.GreenColor),
                prev.OrdersCount == 0 ? null : r.OrdersCount >= prev.OrdersCount),
            new KpiCard("Середній чек", Format.Money(r.AverageCheck), Trend(r.AverageCheck, prev.AverageCheck),
                MaterialIconKind.ScaleBalance, Tones.Blue, Tones.Soft(Tones.BlueColor),
                prev.AverageCheck == 0 ? null : r.AverageCheck >= prev.AverageCheck),
            new KpiCard("Гостей", r.GuestsCount.ToString("N0", Format.Uk), $"Фудкост {Format.Money(r.FoodCost)}",
                MaterialIconKind.AccountMultipleOutline, Tones.Purple, Tones.Soft(Tones.PurpleColor), null),
            new KpiCard("Валова маржа", $"{margin:0} %", "Виручка мінус собівартість",
                MaterialIconKind.TrendingUp, Tones.Yellow, Tones.Soft(Tones.YellowColor), null),
        };

        DailyChart = r.Daily
            .Select(d => new ChartPoint(d.Date.ToString("dd.MM"), (double)d.Revenue,
                $"{d.Date:dd.MM} · {Format.Money(d.Revenue)} · {Format.Plural(d.Orders, "чек", "чеки", "чеків")}"))
            .ToList();

        HourlyChart = Enumerable.Range(10, 14)
            .Select(h => new ChartPoint($"{h}:00", r.OrdersByHour[h],
                $"{h}:00–{h + 1}:00 · {Format.Plural(r.OrdersByHour[h], "чек", "чеки", "чеків")}"))
            .ToList();

        CategorySegments = r.Categories
            .Select(c => new DonutSegment(c.Name, (double)c.Revenue, Color.Parse(c.Color), Format.Money(c.Revenue)))
            .ToList();
        var catTotal = r.Categories.Sum(c => c.Revenue);
        CategoryLegend = r.Categories
            .Select(c => new LegendRow(c.Name, Format.Money(c.Revenue),
                catTotal == 0 ? "0 %" : $"{c.Revenue / catTotal * 100:0} %", Tones.Solid(c.Color)))
            .ToList();

        var maxQty = r.TopDishes.FirstOrDefault()?.Quantity ?? 1;
        TopDishes = r.TopDishes.Take(10)
            .Select((d, i) => new DishReportRow(i + 1, d.Name, d.Category, $"{d.Quantity} шт", Format.Money(d.Revenue),
                (double)d.Quantity / maxQty))
            .ToList();

        var maxRevenue = r.Waiters.FirstOrDefault()?.Revenue ?? 1;
        Waiters = r.Waiters
            .Select(w => new WaiterReportRow(
                string.Concat(w.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])),
                w.Name, Format.Plural(w.Orders, "чек", "чеки", "чеків"), Format.Money(w.Revenue),
                Format.Money(w.AverageCheck), maxRevenue == 0 ? 0 : (double)(w.Revenue / maxRevenue)))
            .ToList();

        OnPropertyChanged(string.Empty);
    }

    private static string Trend(decimal now, decimal before)
    {
        if (before == 0) return "Немає даних для порівняння";
        var delta = (now - before) / before * 100m;
        return $"{(delta >= 0 ? "↑" : "↓")} {Math.Abs(delta):0} % до попер. періоду";
    }

    [RelayCommand]
    private void Export()
    {
        if (_report is null) return;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ватра", "Звіти");
        string? path = null;
        Try(() => path = _reports.ExportCsv(_report, dir));
        if (path is not null)
            Toasts.Success("Звіт експортовано в CSV", path);
    }
}
