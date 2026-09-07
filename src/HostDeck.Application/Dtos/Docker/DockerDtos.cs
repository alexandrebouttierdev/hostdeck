using System;
using System.Collections.Generic;
using HostDeck.Domain.Docker;

namespace HostDeck.Application.Dtos.Docker;

/// <summary>Informations sur le moteur Docker d'un hôte.</summary>
public sealed record DockerHostInfoDto
{
    public required Guid ServerId { get; init; }

    public required string EngineVersion { get; init; }

    public int RunningContainers { get; init; }

    public int TotalContainers { get; init; }

    public int Images { get; init; }
}

/// <summary>Ligne de la table Docker.</summary>
public sealed record DockerContainerSummaryDto
{
    public required string ContainerId { get; init; }

    public required Guid ServerId { get; init; }

    public required string Name { get; init; }

    public required string Image { get; init; }

    public required DockerContainerStatus Status { get; init; }

    public DockerHealthStatus Health { get; init; }

    public int RestartCount { get; init; }

    public TimeSpan? Uptime { get; init; }

    /// <summary>Dernières statistiques connues, absentes si le conteneur est arrêté.</summary>
    public DockerContainerStatsDto? Stats { get; init; }
}

/// <summary>Détail d'un conteneur.</summary>
public sealed record DockerContainerDetailsDto
{
    public required DockerContainerSummaryDto Summary { get; init; }

    public string? Command { get; init; }

    public IReadOnlyList<string> Ports { get; init; } = [];

    public IReadOnlyList<string> Networks { get; init; } = [];

    public IReadOnlyList<string> Volumes { get; init; } = [];

    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>Relevé de consommation d'un conteneur.</summary>
public sealed record DockerContainerStatsDto
{
    public required string ContainerId { get; init; }

    public required DateTimeOffset ObservedAt { get; init; }

    public double CpuPercent { get; init; }

    public long MemoryUsedBytes { get; init; }

    /// <summary>Limite mémoire du conteneur ; zéro signifie « non limité ».</summary>
    public long MemoryLimitBytes { get; init; }

    public double MemoryPercent { get; init; }

    public long NetworkReceivedBytes { get; init; }

    public long NetworkTransmittedBytes { get; init; }

    public long BlockReadBytes { get; init; }

    public long BlockWrittenBytes { get; init; }
}

/// <summary>
/// Ligne de journal d'un conteneur.
///
/// Diffusée en flux et bornée en mémoire : un conteneur bavard produirait sinon des millions
/// de lignes retenues sans limite (§29, §51 T6).
/// </summary>
public readonly record struct ContainerLogEntryDto(
    DateTimeOffset Timestamp,
    ContainerLogChannel Channel,
    string Message);

/// <summary>
/// Flux d'origine d'une ligne de journal. Nommé « Channel » et non « Stream » : ce suffixe
/// est réservé aux dérivés de <see cref="System.IO.Stream" />, ce qui n'est pas le cas ici.
/// </summary>
public enum ContainerLogChannel
{
    StandardOutput = 0,
    StandardError = 1,
}
