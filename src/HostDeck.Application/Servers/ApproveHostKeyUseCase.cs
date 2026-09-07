using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Logging;
using HostDeck.Application.Ports;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Servers;

/// <summary>
/// Enregistre une empreinte de clé d'hôte après décision explicite de l'opérateur.
/// </summary>
public sealed class ApproveHostKeyUseCase
{
    private readonly IHostKeyStore _hostKeys;
    private readonly IValidator<ApproveHostKeyDto> _validator;
    private readonly ILogger<ApproveHostKeyUseCase> _logger;

    public ApproveHostKeyUseCase(
        IHostKeyStore hostKeys,
        IValidator<ApproveHostKeyDto> validator,
        ILogger<ApproveHostKeyUseCase> logger)
    {
        _hostKeys = hostKeys;
        _validator = validator;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ApproveHostKeyDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        await _hostKeys
            .ApproveAsync(request.Host, request.Port, request.Fingerprint, cancellationToken)
            .ConfigureAwait(false);

        ApplicationLog.HostKeyApproved(_logger, request.Host, request.Port);
    }
}
