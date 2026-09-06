using System;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Docker;

/// <summary>
/// Relevé de consommation d'un conteneur à un instant donné.
///
/// La limite mémoire est distincte du total de l'hôte : un conteneur peut être à 95 % de
/// sa propre limite sur une machine par ailleurs peu chargée, et c'est cette proportion-là
/// qui annonce un OOM kill.
/// </summary>
public sealed record DockerContainerStats
{
    public DockerContainerStats(
        DockerContainerId containerId,
        DateTimeOffset observedAt,
        Percentage cpu,
        ByteSize memoryUsed,
        ByteSize memoryLimit,
        ByteSize networkReceived,
        ByteSize networkTransmitted,
        ByteSize blockRead,
        ByteSize blockWritten)
    {
        if (memoryLimit.Bytes > 0 && memoryUsed > memoryLimit)
        {
            throw new DomainValidationException(
                nameof(memoryUsed),
                "la mémoire utilisée ne peut pas dépasser la limite du conteneur");
        }

        ContainerId = containerId;
        ObservedAt = Guard.RequiredInstant(observedAt).ToUniversalTime();
        Cpu = cpu;
        MemoryUsed = memoryUsed;
        MemoryLimit = memoryLimit;
        NetworkReceived = networkReceived;
        NetworkTransmitted = networkTransmitted;
        BlockRead = blockRead;
        BlockWritten = blockWritten;
    }

    public DockerContainerId ContainerId { get; }

    public DateTimeOffset ObservedAt { get; }

    public Percentage Cpu { get; }

    public ByteSize MemoryUsed { get; }

    /// <summary>Limite mémoire du conteneur ; zéro signifie « non limité ».</summary>
    public ByteSize MemoryLimit { get; }

    public ByteSize NetworkReceived { get; }

    public ByteSize NetworkTransmitted { get; }

    public ByteSize BlockRead { get; }

    public ByteSize BlockWritten { get; }

    public bool HasMemoryLimit => MemoryLimit.Bytes > 0;

    /// <summary>
    /// Occupation mémoire rapportée à la limite du conteneur. Sans limite, la proportion
    /// n'a pas de sens et vaut zéro.
    /// </summary>
    public Percentage MemoryRatio =>
        HasMemoryLimit ? Percentage.OfRatio(MemoryUsed.Bytes, MemoryLimit.Bytes) : Percentage.Zero;
}
