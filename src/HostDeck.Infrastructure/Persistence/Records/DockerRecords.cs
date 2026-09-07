using System;

namespace HostDeck.Infrastructure.Persistence.Records;

/// <summary>
/// Ligne de la table <c>docker_hosts</c>.
///
/// Le schéma Docker est créé dès la migration initiale bien que ses dépôts arrivent plus
/// tard : une base déjà en service n'aura pas à subir une migration supplémentaire, et le
/// schéma reste décrit d'un seul tenant.
/// </summary>
internal sealed class DockerHostRecord
{
    public Guid ServerId { get; set; }

    public string EngineVersion { get; set; } = string.Empty;

    public int RunningContainers { get; set; }

    public int TotalContainers { get; set; }

    public int Images { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}

/// <summary>Ligne de la table <c>docker_containers</c>.</summary>
internal sealed class DockerContainerRecord
{
    public string ContainerId { get; set; } = string.Empty;

    public Guid ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public int Status { get; set; }

    public int Health { get; set; }

    public int RestartCount { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}

/// <summary>Ligne de la table <c>docker_metric_samples</c>.</summary>
internal sealed class DockerMetricSampleRecord
{
    public long Id { get; set; }

    public string ContainerId { get; set; } = string.Empty;

    public Guid ServerId { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    public double CpuPercent { get; set; }

    public long MemoryUsedBytes { get; set; }

    public long MemoryLimitBytes { get; set; }

    public long NetworkReceivedBytes { get; set; }

    public long NetworkTransmittedBytes { get; set; }

    public long BlockReadBytes { get; set; }

    public long BlockWrittenBytes { get; set; }
}
