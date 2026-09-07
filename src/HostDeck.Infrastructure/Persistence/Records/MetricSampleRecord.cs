using System;

namespace HostDeck.Infrastructure.Persistence.Records;

/// <summary>
/// Ligne de la table <c>metric_samples</c>.
///
/// <para>
/// Ces enregistrements sont déclarés à EF Core uniquement pour que les migrations créent la
/// table et ses index : le schéma reste ainsi décrit à un seul endroit. Les lectures et
/// écritures passent en revanche par du SQL paramétré brut, car le suivi de changements et la
/// matérialisation d'EF Core sont inadaptés à des centaines de milliers de points (§18, §19).
/// </para>
///
/// <para>
/// Les grandeurs sont stockées à plat plutôt qu'en objets imbriqués : une agrégation en
/// buckets min/avg/max s'exprime directement en SQL sur des colonnes, pas sur une structure.
/// </para>
/// </summary>
internal sealed class MetricSampleRecord
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public double CpuUser { get; set; }

    public double CpuSystem { get; set; }

    public double CpuIoWait { get; set; }

    public double CpuNice { get; set; }

    public double CpuSteal { get; set; }

    public int CoreCount { get; set; }

    public long MemoryTotalBytes { get; set; }

    public long MemoryUsedBytes { get; set; }

    public long MemoryCachedBytes { get; set; }

    public long MemoryBuffersBytes { get; set; }

    public long SwapTotalBytes { get; set; }

    public long SwapUsedBytes { get; set; }

    public double LoadOne { get; set; }

    public double LoadFive { get; set; }

    public double LoadFifteen { get; set; }

    public double UptimeSeconds { get; set; }
}

/// <summary>
/// Ligne de la table <c>disk_metric_samples</c>.
///
/// Un hôte possède plusieurs points de montage : ils sont stockés un par ligne, jamais
/// agrégés, parce que c'est la partition la plus remplie qui déclenche l'incident.
/// </summary>
internal sealed class DiskMetricSampleRecord
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public string MountPoint { get; set; } = string.Empty;

    public string? FileSystem { get; set; }

    public long TotalBytes { get; set; }

    public long UsedBytes { get; set; }
}

/// <summary>
/// Ligne de la table <c>network_metric_samples</c>.
///
/// Les compteurs sont cumulés, comme <c>/proc/net/dev</c> les rapporte. Le débit se dérive de
/// deux relevés successifs ; le stocker directement perdrait l'information brute et rendrait
/// tout recalcul impossible.
/// </summary>
internal sealed class NetworkMetricSampleRecord
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public string InterfaceName { get; set; } = string.Empty;

    public long ReceivedBytes { get; set; }

    public long TransmittedBytes { get; set; }
}
