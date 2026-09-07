using System;
using System.Collections.Generic;

namespace HostDeck.Application.Dtos.Monitoring;

/// <summary>
/// Dernières valeurs connues d'un hôte : ce que la table Infrastructure affiche dans
/// chaque ligne, à côté des sparklines.
/// </summary>
public sealed record LatestMetricDto
{
    public required Guid ServerId { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public double CpuPercent { get; init; }

    public double MemoryPercent { get; init; }

    /// <summary>Occupation du point de montage le plus rempli.</summary>
    public double DiskPercent { get; init; }

    public double LoadOneMinute { get; init; }

    public double NetworkReceivedBytesPerSecond { get; init; }

    public double NetworkTransmittedBytesPerSecond { get; init; }

    public TimeSpan Uptime { get; init; }
}

/// <summary>
/// Grandeur traçable sur un graphique. Distincte de
/// <see cref="Domain.Alerts.MonitoredMetric"/>, qui décrit ce qu'une règle peut surveiller :
/// on trace des séries qui ne déclenchent aucune alerte, et l'inverse est vrai aussi.
/// </summary>
public enum MetricSeriesKind
{
    CpuUser = 0,
    CpuSystem = 1,
    CpuIoWait = 2,
    CpuNice = 3,
    CpuSteal = 4,
    CpuTotal = 5,
    MemoryUsed = 6,
    MemoryCached = 7,
    MemoryBuffers = 8,
    MemoryFree = 9,
    SwapUsed = 10,
    DiskUsed = 11,
    LoadOne = 12,
    LoadFive = 13,
    LoadFifteen = 14,
    NetworkReceived = 15,
    NetworkTransmitted = 16,
}

/// <summary>
/// Demande d'historique.
///
/// <see cref="MaxPoints"/> est dimensionné par la largeur du graphique : le dépôt agrège
/// côté SQL plutôt que de charger des centaines de milliers de points pour en tracer quelques
/// centaines (§19).
/// </summary>
public sealed record MetricHistoryRequestDto
{
    public required Guid ServerId { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required IReadOnlyList<MetricSeriesKind> Series { get; init; }

    /// <summary>Nombre maximal de points par série, généralement la largeur du tracé en pixels.</summary>
    public int MaxPoints { get; init; } = 600;
}

/// <summary>
/// Un point agrégé.
///
/// Porte min, moyenne et max plutôt qu'une seule valeur : après regroupement en buckets, ne
/// conserver que la moyenne effacerait les pics, c'est-à-dire précisément ce qu'on cherche
/// dans un graphique de supervision (§19).
/// </summary>
public readonly record struct MetricPointDto(
    DateTimeOffset Timestamp,
    double Minimum,
    double Average,
    double Maximum);

/// <summary>Série temporelle d'une grandeur sur une plage.</summary>
public sealed record MetricSeriesDto
{
    public required MetricSeriesKind Kind { get; init; }

    public required IReadOnlyList<MetricPointDto> Points { get; init; }
}

/// <summary>Historique renvoyé pour une plage et un jeu de séries.</summary>
public sealed record MetricHistoryDto
{
    public required Guid ServerId { get; init; }

    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>Largeur du bucket d'agrégation effectivement appliqué.</summary>
    public required TimeSpan BucketSize { get; init; }

    public required IReadOnlyList<MetricSeriesDto> Series { get; init; }
}

/// <summary>
/// Statistiques d'une grandeur sur une plage : les colonnes « Actuel / Min / Moy / Max »
/// de l'écran Données en direct.
/// </summary>
public sealed record MetricStatisticsDto
{
    public required MetricSeriesKind Kind { get; init; }

    public double? Current { get; init; }

    public double Minimum { get; init; }

    public double Average { get; init; }

    public double Maximum { get; init; }

    public int SampleCount { get; init; }
}
