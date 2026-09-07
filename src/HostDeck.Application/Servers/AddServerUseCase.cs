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
/// Enregistre un nouveau serveur et confie son secret au trousseau du système.
/// </summary>
public sealed class AddServerUseCase
{
    private readonly IServerRepository _servers;
    private readonly ICredentialStore _credentials;
    private readonly IValidator<CreateServerDto> _validator;
    private readonly ILogger<AddServerUseCase> _logger;

    public AddServerUseCase(
        IServerRepository servers,
        ICredentialStore credentials,
        IValidator<CreateServerDto> validator,
        ILogger<AddServerUseCase> logger)
    {
        _servers = servers;
        _credentials = credentials;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServerSummaryDto> ExecuteAsync(
        CreateServerDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var address = HostAddress.Parse(request.Address);
        var port = new Port(request.Port);

        if (await _servers.ExistsWithEndpointAsync(address, port, null, cancellationToken).ConfigureAwait(false))
        {
            throw new ValidationException(
                $"Un serveur utilise déjà l'adresse {address}:{port}.");
        }

        var serverId = ServerId.New();
        var credential = CredentialReference.ForServer(serverId, request.CredentialKind);

        var server = new Server(
            serverId,
            new ServerName(request.Name),
            address,
            port,
            new SshUsername(request.Username),
            credential,
            MonitoringInterval.FromSeconds(request.MonitoringIntervalSeconds),
            ServerMapper.ToDomain(request.JumpHost),
            ServerMapper.ToGroup(request.Group),
            ServerMapper.ToTags(request.Tags),
            request.DockerEnabled);

        // Le secret part au trousseau avant l'écriture en base : si le trousseau refuse,
        // aucun serveur orphelin — c'est-à-dire sans identifiant utilisable — n'est créé.
        await _credentials
            .WriteAsync(credential, request.Secret.AsMemory(), cancellationToken)
            .ConfigureAwait(false);

        CredentialReference? passphraseReference = null;
        if (!string.IsNullOrEmpty(request.Passphrase))
        {
            passphraseReference = new CredentialReference($"{credential.Key}:passphrase", credential.Kind);
            await _credentials
                .WriteAsync(passphraseReference, request.Passphrase.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            await _servers.AddAsync(server, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // L'écriture en base a échoué : on retire le secret pour ne pas laisser une
            // entrée de trousseau que plus rien ne référence.
            await TryRemoveOrphanCredentialAsync(credential).ConfigureAwait(false);
            if (passphraseReference is not null)
            {
                await TryRemoveOrphanCredentialAsync(passphraseReference).ConfigureAwait(false);
            }

            throw;
        }

        // Journalisation volontairement limitée à des identifiants : jamais de secret (§51, T2).
        ApplicationLog.ServerAdded(_logger, serverId.Value, address.Value, port.Value, server.ConnectionMode);

        return ServerMapper.ToSummary(server);
    }

    private async Task TryRemoveOrphanCredentialAsync(CredentialReference credential)
    {
        try
        {
            // Le jeton d'annulation d'origine peut déjà être annulé ; ce nettoyage doit
            // aboutir malgré tout, sous son propre délai borné.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _credentials.DeleteAsync(credential, cleanup.Token).ConfigureAwait(false);
        }
        catch (CredentialException exception)
        {
            ApplicationLog.OrphanCredentialNotRemoved(_logger, exception, credential.Key);
        }
        catch (OperationCanceledException exception)
        {
            ApplicationLog.OrphanCredentialNotRemoved(_logger, exception, credential.Key);
        }
    }
}
