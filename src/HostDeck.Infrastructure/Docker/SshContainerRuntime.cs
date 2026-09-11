using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Docker;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Servers;

namespace HostDeck.Infrastructure.Docker;

/// <summary>
/// Moteur Docker distant via SSH et le CLI Docker.
/// </summary>
internal sealed class SshContainerRuntime : IContainerRuntime
{
    private readonly IServerRepository _servers;
    private readonly ISshConnectionFactory _ssh;

    public SshContainerRuntime(IServerRepository servers, ISshConnectionFactory ssh)
    {
        _servers = servers;
        _ssh = ssh;
    }

    public Task<DockerHost> GetInfoAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            serverId,
            DockerCliCommands.Info,
            static (output, server) => DockerCliParsers.ParseInfo(output, server.Id),
            "get-info",
            cancellationToken);

    public Task<IReadOnlyList<DockerContainer>> ListContainersAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            serverId,
            DockerCliCommands.ListContainers,
            static (output, server) => DockerCliParsers.ParseContainerList(output, server.Id),
            "list-containers",
            cancellationToken);

    public Task<DockerContainerDetailsDto> GetContainerDetailsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            serverId,
            DockerCliCommands.Inspect(containerId.Value),
            static (output, server, id) => DockerCliParsers.ParseInspect(output, server.Id, id),
            "get-container-details",
            containerId,
            cancellationToken);

    public Task<DockerContainerStats> GetStatsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            serverId,
            DockerCliCommands.Stats(containerId.Value),
            static (output, _, id) => DockerCliParsers.ParseStats(output, id),
            "get-stats",
            containerId,
            cancellationToken);

    public Task StartContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        CancellationToken cancellationToken = default)
        => ExecuteMutationAsync(
            serverId,
            DockerCliCommands.Start(containerId.Value),
            "start-container",
            cancellationToken);

    public Task StopContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default)
        => ExecuteMutationAsync(
            serverId,
            DockerCliCommands.Stop(containerId.Value, ToGraceSeconds(gracePeriod)),
            "stop-container",
            cancellationToken);

    public Task RestartContainerAsync(
        ServerId serverId,
        DockerContainerId containerId,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken = default)
        => ExecuteMutationAsync(
            serverId,
            DockerCliCommands.Restart(containerId.Value, ToGraceSeconds(gracePeriod)),
            "restart-container",
            cancellationToken);

    public async IAsyncEnumerable<ContainerLogEntryDto> StreamLogsAsync(
        ServerId serverId,
        DockerContainerId containerId,
        ContainerLogOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var server = await RequireDockerServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _ssh.ConnectAsync(server, cancellationToken).ConfigureAwait(false);

        var command = DockerCliCommands.LogsTail(
            options.TailLines,
            timestamps: true,
            containerId.Value);

        var result = await ExecuteRequiredAsync(connection, command, "stream-logs", cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in DockerCliParsers.ParseLogs(result.StandardOutput))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }

        // Le suivi en direct (-f) n'est pas fiable via une commande SSH unique : l'appelant
        // relance StreamLogsAsync ou interroge les journaux ponctuellement.
        _ = options.Follow;
    }

    private async Task<T> ExecuteAsync<T>(
        ServerId serverId,
        string command,
        Func<string, Server, T> map,
        string operation,
        CancellationToken cancellationToken)
    {
        var server = await RequireDockerServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _ssh.ConnectAsync(server, cancellationToken).ConfigureAwait(false);
        var result = await ExecuteRequiredAsync(connection, command, operation, cancellationToken)
            .ConfigureAwait(false);
        return map(result.StandardOutput, server);
    }

    private async Task<T> ExecuteAsync<T>(
        ServerId serverId,
        string command,
        Func<string, Server, DockerContainerId, T> map,
        string operation,
        DockerContainerId containerId,
        CancellationToken cancellationToken)
    {
        var server = await RequireDockerServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _ssh.ConnectAsync(server, cancellationToken).ConfigureAwait(false);
        var result = await ExecuteRequiredAsync(connection, command, operation, cancellationToken)
            .ConfigureAwait(false);
        return map(result.StandardOutput, server, containerId);
    }

    private async Task ExecuteMutationAsync(
        ServerId serverId,
        string command,
        string operation,
        CancellationToken cancellationToken)
    {
        var server = await RequireDockerServerAsync(serverId, cancellationToken).ConfigureAwait(false);
        await using var connection = await _ssh.ConnectAsync(server, cancellationToken).ConfigureAwait(false);
        await ExecuteRequiredAsync(connection, command, operation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Server> RequireDockerServerAsync(
        ServerId serverId,
        CancellationToken cancellationToken)
    {
        var server = await _servers.FindAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), serverId.Value.ToString());

        if (!server.DockerEnabled)
        {
            throw new DockerException(
                "require-docker-enabled",
                $"Docker n'est pas activé pour le serveur {server.Name.Value}");
        }

        return server;
    }

    private static async Task<CommandResult> ExecuteRequiredAsync(
        ISshConnection connection,
        string command,
        string operation,
        CancellationToken cancellationToken)
    {
        var result = await connection.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"code de sortie {result.ExitCode}"
                : result.StandardError.Trim();
            throw new DockerException(operation, detail);
        }

        return result;
    }

    private static int ToGraceSeconds(TimeSpan gracePeriod)
    {
        if (gracePeriod < TimeSpan.Zero)
        {
            throw new DockerException("validate-grace-period", "le délai d'arrêt ne peut pas être négatif");
        }

        var seconds = (int)Math.Ceiling(gracePeriod.TotalSeconds);
        return Math.Clamp(seconds, 0, 300);
    }
}
