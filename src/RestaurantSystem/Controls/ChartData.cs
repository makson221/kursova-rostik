using Avalonia.Media;

namespace RestaurantSystem.Controls;

/// <summary>Точка стовпчикової діаграми.</summary>
public sealed record ChartPoint(string Label, double Value, string Tooltip);

/// <summary>Сегмент кільцевої діаграми.</summary>
public sealed record DonutSegment(string Label, double Value, Color Color, string ValueText);
