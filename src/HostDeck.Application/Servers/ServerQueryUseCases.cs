using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Errors;
using HostDeck.Application.Logging;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Servers;

/// <summary>
/// Renvoie l'inventaire complet, chaque hôte accompagné de ses dernières valeurs.
/// </summary>
public sealed class GetServersUseCase
{
    private readonly IServerRepository _servers;
    private readonly IMetricsRepository _metrics;

    public GetServersUseCase(IServerRepository servers, IMetricsRepository metrics)
    {
        _servers = servers;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<ServerSummaryDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var servers = await _servers.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Une seule requête pour toutes les dernières valeurs, plutôt qu'une par hôte :
        // une centaine d'allers-retours SQLite à chaque affichage de l'inventaire
        // se verrait immédiatement à l'écran (§37).
        var latest = await _metrics.GetLatestForAllAsync(cancellationToken).ConfigureAwait(false);
        var latestByServer = latest.ToDictionary(metric => metric.ServerId);

        return
        [
            .. servers.Select(server => ServerMapper.ToSummary(
                server,
                latestByServer.GetValueOrDefault(server.Id.Value)))
        ];
    }
}

/// <summary>
/// Renvoie le détail complet d'un hôte.
/// </summary>
public sealed class GetServerDetailsUseCase
{
    private readonly IServerRepository _servers;
    private readonly IMetricsRepository _metrics;
    private readonly IIncidentRepository _incidents;

    public GetServerDetailsUseCase(
        IServerRepository servers,
        IMetricsRepository metrics,
        IIncidentRepository incidents)
    {
        _servers = servers;
        _metrics = metrics;
        _incidents = incidents;
    }

    public async Task<ServerDetailsDto> ExecuteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var id = new ServerId(serverId);

        var server = await _servers.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), serverId.ToString());

        var latest = await _metrics.GetLatestAsync(id, cancellationToken).ConfigureAwait(false);
        var active = await _incidents.GetActiveAsync(cancellationToken).ConfigureAwait(false);

        return new ServerDetailsDto
        {
            Summary = ServerMapper.ToSummary(server, latest),
            Configuration = ServerMapper.ToConfiguration(server),
            Uptime = latest?.Uptime,
            ActiveIncidents = active.Count(incident => incident.ServerId == id),
        };
    }
}

/// <summary>
/// Supprime un serveur, ses données et son secret.
/// </summary>
public sealed class DeleteServerUseCase
{
    private readonly IServerRepository _servers;
    private readonly ICredentialStore _credentials;
    private readonly ILogger<DeleteServerUseCase> _logger;

    public DeleteServerUseCase(
        IServerRepository servers,
        ICredentialStore credentials,
        ILogger<DeleteServerUseCase> logger)
    {
        _servers = servers;
        _credentials = credentials;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var id = new ServerId(serverId);

        var server = await _servers.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), serverId.ToString());

        await _servers.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        // Le secret est retiré après la suppression en base. Si le trousseau échoue ici, le
        // serveur est bien supprimé et l'entrée résiduelle est signalée : c'est préférable à
        // laisser en place un serveur dont l'identifiant a déjà disparu.
        try
        {
            await _credentials.DeleteAsync(server.Credential, cancellationToken).ConfigureAwait(false);
        }
        catch (CredentialException exception)
        {
            ApplicationLog.CredentialLeftBehind(_logger, exception, serverId, server.Credential.Key);
        }

        ApplicationLog.ServerDeleted(_logger, serverId);
    }
}
