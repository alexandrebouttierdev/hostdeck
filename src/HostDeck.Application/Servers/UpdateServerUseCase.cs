using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Errors;
using HostDeck.Application.Logging;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Servers;

/// <summary>
/// Modifie la configuration d'un serveur existant.
///
/// Ne touche jamais au secret : changer un identifiant est une opération distincte, pour
/// qu'une mise à jour de configuration ne puisse pas en écraser un par inadvertance (§30).
/// </summary>
public sealed class UpdateServerUseCase
{
    private readonly IServerRepository _servers;
    private readonly IValidator<UpdateServerDto> _validator;
    private readonly ILogger<UpdateServerUseCase> _logger;

    public UpdateServerUseCase(
        IServerRepository servers,
        IValidator<UpdateServerDto> validator,
        ILogger<UpdateServerUseCase> logger)
    {
        _servers = servers;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServerSummaryDto> ExecuteAsync(
        UpdateServerDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var serverId = new ServerId(request.ServerId);
        var server = await _servers.FindAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), request.ServerId.ToString());

        var address = HostAddress.Parse(request.Address);
        var port = new Port(request.Port);

        var endpointTaken = await _servers
            .ExistsWithEndpointAsync(address, port, serverId, cancellationToken)
            .ConfigureAwait(false);

        if (endpointTaken)
        {
            throw new ValidationException($"Un autre serveur utilise déjà l'adresse {address}:{port}.");
        }

        // Le bastion est appliqué avant le point de connexion, et l'ordre compte. Déplacer
        // un serveur sur l'ancienne adresse de son bastion tout en le rattachant à un
        // nouveau bastion est légitime ; l'ordre inverse le rejetterait à tort, en comparant
        // la nouvelle adresse à l'ancien bastion pas encore remplacé.
        server.ChangeJumpHost(ServerMapper.ToDomain(request.JumpHost));
        server.ChangeEndpoint(address, port, new SshUsername(request.Username));
        server.Rename(new ServerName(request.Name));
        server.ChangeMonitoringInterval(MonitoringInterval.FromSeconds(request.MonitoringIntervalSeconds));
        server.MoveToGroup(ServerMapper.ToGroup(request.Group));
        server.SetDockerEnabled(request.DockerEnabled);
        server.ReplaceTags(ServerMapper.ToTags(request.Tags));

        await _servers.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

        ApplicationLog.ServerUpdated(_logger, serverId.Value);

        return ServerMapper.ToSummary(server);
    }
}
