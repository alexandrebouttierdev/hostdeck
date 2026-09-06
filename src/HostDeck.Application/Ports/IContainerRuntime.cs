using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Docker;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Ports;

/// <summary>
/// Moteur de conteneurs d'un hôte supervisé.
///
/// Abstraction volontairement neutre vis-à-vis de Docker : c'est ce qui permettra d'ajouter
/// Podman sans toucher aux couches supérieures (§13, §56). Les types du SDK Docker restent
/// confinés à l'Infrastructure.
/// </summary>
public interface IContainerRuntime
{
    Task<DockerHost> GetInfoAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DockerContainer>> ListContainersAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default);

    Task<DockerContainerDetailsDto> GetContainerDetailsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default);

    Task<DockerContainerStats> GetStatsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default);

    Task StartContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrête un conteneur en lui laissant <paramref name="gracePeriod"/> pour se terminer
    /// avant d'être tué.
    /// </summary>
    Task StopContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default);

    Task RestartContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Diffuse les journaux d'un conteneur.
    ///
    /// Un flux, et non une liste : un conteneur bavard produirait des millions de lignes.
    /// L'appelant consomme au rythme qu'il souhaite et interrompt par le jeton d'annulation,
    /// ce qui ferme le flux distant (§29, §50).
    /// </summary>
    IAsyncEnumerable<ContainerLogEntryDto> StreamLogsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        ContainerLogOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>Options de lecture des journaux d'un conteneur.</summary>
public sealed record ContainerLogOptions
{
    /// <summary>Nombre de lignes d'historique à envoyer avant de suivre le flux en direct.</summary>
    public int TailLines { get; init; } = 200;

    /// <summary>Continue à émettre les nouvelles lignes après l'historique.</summary>
    public bool Follow { get; init; } = true;

    public bool IncludeStandardOutput { get; init; } = true;

    public bool IncludeStandardError { get; init; } = true;

    /// <summary>Ne renvoie que les lignes postérieures à cet instant.</summary>
    public DateTimeOffset? Since { get; init; }
}
