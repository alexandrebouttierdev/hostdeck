using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Presentation.Controls.Charts;

namespace HostDeck.Presentation.Charts;

/// <summary>
/// Convertit l'historique Application en séries Presentation. Aucune valeur inventée :
/// une série sans points reste absente.
/// </summary>
public static class MetricSeriesMapper
{
    public static readonly Color CpuColor = Color.Parse("#2F8FEF");
    public static readonly Color MemoryColor = Color.Parse("#09DE77");
    public static readonly Color DiskColor = Color.Parse("#FF9A3C");
    public static readonly Color NetworkInColor = Color.Parse("#2F8FEF");
    public static readonly Color NetworkOutColor = Color.Parse("#09DE77");
    public static readonly Color LoadColor = Color.Parse("#E5B84B");

    public static IReadOnlyList<float>? ToValues(MetricSeriesDto? series)
    {
        if (series is null || series.Points.Count == 0)
        {
            return null;
        }

        return series.Points.Select(point => (float)point.Average).ToArray();
    }

    public static ChartSeriesData? ToChartSeries(MetricSeriesDto? series, string name, Color color)
    {
        var values = ToValues(series);
        if (values is null || values.Count < 2)
        {
            return null;
        }

        return new ChartSeriesData
        {
            Name = name,
            Values = values,
            Color = color,
        };
    }

    public static MetricSeriesDto? Find(MetricHistoryDto history, MetricSeriesKind kind) =>
        history.Series.FirstOrDefault(series => series.Kind == kind);

    public static IReadOnlyList<float> AppendBounded(
        IReadOnlyList<float>? existing,
        float value,
        int maxPoints)
    {
        var list = existing is null ? new List<float>() : existing.ToList();
        list.Add(value);
        if (list.Count > maxPoints)
        {
            list.RemoveRange(0, list.Count - maxPoints);
        }

        return list;
    }
}
