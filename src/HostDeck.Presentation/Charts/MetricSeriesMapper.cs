using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Presentation.Controls.Charts;

namespace HostDeck.Presentation.Charts;

/// <summary>
/// Convertit l'historique Application en séries Presentation. Aucune valeur inventée :
/// une série sans points reste absente. Couleurs alignées sur les tokens Themes Series*.
/// </summary>
public static class MetricSeriesMapper
{
    // Hex identiques aux tokens Colors.axaml (SeriesCpu*, SeriesMemory*, …).
    public static readonly Color CpuColor = Color.Parse("#2F8FEF");
    public static readonly Color CpuUserColor = Color.Parse("#2F8FEF");
    public static readonly Color CpuSystemColor = Color.Parse("#E5B84B");
    public static readonly Color CpuIoWaitColor = Color.Parse("#FB4662");
    public static readonly Color CpuNiceColor = Color.Parse("#A16BE0");
    public static readonly Color CpuStealColor = Color.Parse("#8FA3B8");
    public static readonly Color MemoryColor = Color.Parse("#09DE77");
    public static readonly Color DiskColor = Color.Parse("#FF9A3C");
    public static readonly Color NetworkInColor = Color.Parse("#2F8FEF");
    public static readonly Color NetworkOutColor = Color.Parse("#09DE77");
    public static readonly Color LoadColor = Color.Parse("#E5B84B");

    public static readonly MetricSeriesKind[] CpuStackKinds =
    [
        MetricSeriesKind.CpuUser,
        MetricSeriesKind.CpuSystem,
        MetricSeriesKind.CpuIoWait,
        MetricSeriesKind.CpuNice,
        MetricSeriesKind.CpuSteal,
    ];

    public static IReadOnlyList<float>? ToValues(MetricSeriesDto? series)
    {
        if (series is null || series.Points.Count == 0)
        {
            return null;
        }

        return series.Points.Select(point => (float)point.Average).ToArray();
    }

    public static IReadOnlyList<DateTimeOffset>? ToTimestamps(MetricSeriesDto? series)
    {
        if (series is null || series.Points.Count == 0)
        {
            return null;
        }

        return series.Points.Select(point => point.Timestamp).ToArray();
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
            Timestamps = ToTimestamps(series),
        };
    }

    public static MetricSeriesDto? Find(MetricHistoryDto history, MetricSeriesKind kind) =>
        history.Series.FirstOrDefault(series => series.Kind == kind);

    public static Color ColorForKind(MetricSeriesKind kind) => kind switch
    {
        MetricSeriesKind.CpuUser => CpuUserColor,
        MetricSeriesKind.CpuSystem => CpuSystemColor,
        MetricSeriesKind.CpuIoWait => CpuIoWaitColor,
        MetricSeriesKind.CpuNice => CpuNiceColor,
        MetricSeriesKind.CpuSteal => CpuStealColor,
        MetricSeriesKind.CpuTotal => CpuColor,
        MetricSeriesKind.MemoryUsed => MemoryColor,
        MetricSeriesKind.MemoryCached => Color.Parse("#2F8FEF"),
        MetricSeriesKind.MemoryBuffers => Color.Parse("#E5B84B"),
        MetricSeriesKind.MemoryFree => Color.Parse("#7B5BD6"),
        MetricSeriesKind.DiskUsed => DiskColor,
        MetricSeriesKind.NetworkReceived => NetworkInColor,
        MetricSeriesKind.NetworkTransmitted => NetworkOutColor,
        MetricSeriesKind.LoadOne or MetricSeriesKind.LoadFive or MetricSeriesKind.LoadFifteen => LoadColor,
        _ => CpuColor,
    };

    public static string DisplayNameForKind(MetricSeriesKind kind) => kind switch
    {
        MetricSeriesKind.CpuUser => "user",
        MetricSeriesKind.CpuSystem => "system",
        MetricSeriesKind.CpuIoWait => "iowait",
        MetricSeriesKind.CpuNice => "nice",
        MetricSeriesKind.CpuSteal => "steal",
        MetricSeriesKind.CpuTotal => "CPU",
        MetricSeriesKind.MemoryUsed => "Mémoire",
        MetricSeriesKind.DiskUsed => "Disque",
        MetricSeriesKind.NetworkReceived => "Entrant",
        MetricSeriesKind.NetworkTransmitted => "Sortant",
        MetricSeriesKind.LoadOne => "Load 1",
        MetricSeriesKind.LoadFive => "Load 5",
        MetricSeriesKind.LoadFifteen => "Load 15",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Construit les séries CPU empilables présentes dans l'historique (jamais inventées).
    /// Retourne null si aucune mode n'a assez de points ; l'appelant peut alors basculer sur CpuTotal.
    /// </summary>
    public static IReadOnlyList<ChartSeriesData>? ToCpuStackSeries(
        MetricHistoryDto history,
        Func<MetricSeriesDto?, MetricSeriesDto?>? transform = null)
    {
        var list = new List<ChartSeriesData>(CpuStackKinds.Length);
        foreach (var kind in CpuStackKinds)
        {
            var raw = Find(history, kind);
            var series = transform is null ? raw : transform(raw);
            var chart = ToChartSeries(series, DisplayNameForKind(kind), ColorForKind(kind));
            if (chart is not null)
            {
                list.Add(chart);
            }
        }

        return list.Count == 0 ? null : list;
    }

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

    public static IReadOnlyList<DateTimeOffset> AppendBoundedTimestamps(
        IReadOnlyList<DateTimeOffset>? existing,
        DateTimeOffset timestamp,
        int maxPoints)
    {
        var list = existing is null ? new List<DateTimeOffset>() : existing.ToList();
        list.Add(timestamp);
        if (list.Count > maxPoints)
        {
            list.RemoveRange(0, list.Count - maxPoints);
        }

        return list;
    }
}
