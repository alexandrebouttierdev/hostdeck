using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Docker;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Docker;

/// <summary>Liste les conteneurs des hôtes Docker activés.</summary>
public sealed class ListDockerContainersUseCase
{
    private readonly IServerRepository _servers;
    private readonly IContainerRuntime _runtime;

    public ListDockerContainersUseCase(IServerRepository servers, IContainerRuntime runtime)
    {
        _servers = servers;
        _runtime = runtime;
    }

    public async Task<IReadOnlyList<DockerContainerSummaryDto>> ExecuteAsync(
        Guid? serverId = null,
        CancellationToken cancellationToken = default)
    {
        var allServers = await _servers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var targets = allServers.Where(server => server.DockerEnabled);

        if (serverId is Guid filterId)
        {
            targets = targets.Where(server => server.Id.Value == filterId);
        }

        var summaries = new List<DockerContainerSummaryDto>();
        foreach (var server in targets)
        {
            var containers = await _runtime.ListContainersAsync(server.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var container in containers)
            {
                DockerContainerStatsDto? stats = null;
                if (container.IsRunning)
                {
                    stats = await TryGetStatsDtoAsync(server.Id, container.Id, cancellationToken)
                        .ConfigureAwait(false);
                }

                summaries.Add(DockerMapper.ToSummaryDto(container, stats));
            }
        }

        return summaries;
    }

    private async Task<DockerContainerStatsDto?> TryGetStatsDtoAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _runtime.GetStatsAsync(serverId, containerId, cancellationToken)
                .ConfigureAwait(false);
            return DockerMapper.ToStatsDto(stats);
        }
        catch (DockerException)
        {
            return null;
        }
    }
}

/// <summary>Informations sur le moteur Docker d'un hôte.</summary>
public sealed class GetDockerHostInfoUseCase
{
    private readonly IServerRepository _servers;
    private readonly IContainerRuntime _runtime;

    public GetDockerHostInfoUseCase(IServerRepository servers, IContainerRuntime runtime)
    {
        _servers = servers;
        _runtime = runtime;
    }

    public async Task<DockerHostInfoDto> ExecuteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        await RequireDockerEnabledAsync(serverId, cancellationToken).ConfigureAwait(false);

        var host = await _runtime
            .GetInfoAsync(new ServerId(serverId), cancellationToken)
            .ConfigureAwait(false);

        return DockerMapper.ToHostInfoDto(host);
    }

    private async Task RequireDockerEnabledAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var server = await _servers.FindAsync(new ServerId(serverId), cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), serverId.ToString());

        if (!server.DockerEnabled)
        {
            throw new DockerException(
                "require-docker-enabled",
                $"Docker n'est pas activé pour le serveur {server.Name.Value}");
        }
    }
}

/// <summary>Relevé de consommation d'un conteneur.</summary>
public sealed class GetContainerStatsUseCase
{
    private readonly IContainerRuntime _runtime;

    public GetContainerStatsUseCase(IContainerRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<DockerContainerStatsDto> ExecuteAsync(
        Guid serverId,
        string containerId,
        CancellationToken cancellationToken = default)
    {
        var stats = await _runtime
            .GetStatsAsync(new ServerId(serverId), new DockerContainerId(containerId), cancellationToken)
            .ConfigureAwait(false);

        return DockerMapper.ToStatsDto(stats);
    }
}

/// <summary>Démarre un conteneur arrêté.</summary>
public sealed class StartDockerContainerUseCase
{
    private readonly IContainerRuntime _runtime;

    public StartDockerContainerUseCase(IContainerRuntime runtime)
    {
        _runtime = runtime;
    }

    public Task ExecuteAsync(
        Guid serverId,
        string containerId,
        CancellationToken cancellationToken = default)
        => _runtime.StartContainerAsync(
            new ServerId(serverId),
            new DockerContainerId(containerId),
            cancellationToken);
}

/// <summary>Arrête un conteneur en cours d'exécution.</summary>
public sealed class StopDockerContainerUseCase
{
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(10);
    private readonly IContainerRuntime _runtime;

    public StopDockerContainerUseCase(IContainerRuntime runtime)
    {
        _runtime = runtime;
    }

    public Task ExecuteAsync(
        Guid serverId,
        string containerId,
        TimeSpan? gracePeriod = null,
        CancellationToken cancellationToken = default)
        => _runtime.StopContainerAsync(
            new ServerId(serverId),
            new DockerContainerId(containerId),
            gracePeriod ?? DefaultGracePeriod,
            cancellationToken);
}

/// <summary>Redémarre un conteneur.</summary>
public sealed class RestartDockerContainerUseCase
{
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(10);
    private readonly IContainerRuntime _runtime;

    public RestartDockerContainerUseCase(IContainerRuntime runtime)
    {
        _runtime = runtime;
    }

    public Task ExecuteAsync(
        Guid serverId,
        string containerId,
        TimeSpan? gracePeriod = null,
        CancellationToken cancellationToken = default)
        => _runtime.RestartContainerAsync(
            new ServerId(serverId),
            new DockerContainerId(containerId),
            gracePeriod ?? DefaultGracePeriod,
            cancellationToken);
}

/// <summary>Projection domaine → DTO Docker.</summary>
internal static class DockerMapper
{
    public static DockerHostInfoDto ToHostInfoDto(DockerHost host) =>
        new()
        {
            ServerId = host.ServerId.Value,
            EngineVersion = host.EngineVersion,
            RunningContainers = host.RunningContainers,
            TotalContainers = host.TotalContainers,
            Images = host.Images,
        };

    public static DockerContainerSummaryDto ToSummaryDto(
        DockerContainer container,
        DockerContainerStatsDto? stats = null) =>
        new()
        {
            ContainerId = container.Id.Value,
            ServerId = container.ServerId.Value,
            Name = container.Name,
            Image = container.Image,
            Status = container.Status,
            Health = container.Health,
            RestartCount = container.RestartCount,
            Uptime = container.UptimeAt(DateTimeOffset.UtcNow),
            Stats = stats,
        };

    public static DockerContainerStatsDto ToStatsDto(DockerContainerStats stats) =>
        new()
        {
            ContainerId = stats.ContainerId.Value,
            ObservedAt = stats.ObservedAt,
            CpuPercent = stats.Cpu.Value,
            MemoryUsedBytes = stats.MemoryUsed.Bytes,
            MemoryLimitBytes = stats.MemoryLimit.Bytes,
            MemoryPercent = stats.MemoryRatio.Value,
            NetworkReceivedBytes = stats.NetworkReceived.Bytes,
            NetworkTransmittedBytes = stats.NetworkTransmitted.Bytes,
            BlockReadBytes = stats.BlockRead.Bytes,
            BlockWrittenBytes = stats.BlockWritten.Bytes,
        };
}
