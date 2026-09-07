using System;
using System.Diagnostics;
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
/// Teste une configuration SSH avant enregistrement, en distinguant les causes d'échec (§30).
/// </summary>
public sealed class TestConnectionUseCase
{
    /// <summary>Commande fixe de sonde — jamais assemblée depuis une saisie (§51, T4).</summary>
    private const string ProbeCommand = "uname -s";

    private readonly ISshConnectionFactory _ssh;
    private readonly ICredentialStore _credentials;
    private readonly IValidator<TestConnectionRequestDto> _validator;
    private readonly ILogger<TestConnectionUseCase> _logger;

    public TestConnectionUseCase(
        ISshConnectionFactory ssh,
        ICredentialStore credentials,
        IValidator<TestConnectionRequestDto> validator,
        ILogger<TestConnectionUseCase> logger)
    {
        _ssh = ssh;
        _credentials = credentials;
        _validator = validator;
        _logger = logger;
    }

    public async Task<TestConnectionResultDto> ExecuteAsync(
        TestConnectionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(request.Secret))
        {
            return await ExecuteWithInlineSecretAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteCoreAsync(request, request.CredentialKey!, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TestConnectionResultDto> ExecuteWithInlineSecretAsync(
        TestConnectionRequestDto request,
        CancellationToken cancellationToken)
    {
        var tempKey = $"hostdeck:connection-test:{Guid.NewGuid():D}";
        var credential = new CredentialReference(tempKey, request.CredentialKind);

        await _credentials
            .WriteAsync(credential, request.Secret!.AsMemory(), cancellationToken)
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
            return await ExecuteCoreAsync(request, tempKey, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await TryDeleteAsync(credential).ConfigureAwait(false);
            if (passphraseReference is not null)
            {
                await TryDeleteAsync(passphraseReference).ConfigureAwait(false);
            }
        }
    }

    private async Task<TestConnectionResultDto> ExecuteCoreAsync(
        TestConnectionRequestDto request,
        string credentialKey,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var serverId = ServerId.New();

        var server = new Server(
            serverId,
            new ServerName("connection-test"),
            HostAddress.Parse(request.Address),
            new Port(request.Port),
            new SshUsername(request.Username),
            new CredentialReference(credentialKey, request.CredentialKind),
            MonitoringInterval.Default,
            ServerMapper.ToDomain(request.JumpHost));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        try
        {
            await using var connection = await _ssh.ConnectAsync(server, timeout.Token).ConfigureAwait(false);

            var probe = await connection.ExecuteAsync(ProbeCommand, timeout.Token).ConfigureAwait(false);
            var os = probe.IsSuccess ? probe.StandardOutput.Trim() : null;

            var result = TestConnectionResultDto.Success(
                stopwatch.Elapsed,
                connection.Latency,
                operatingSystem: string.IsNullOrWhiteSpace(os) ? null : os);

            ApplicationLog.ConnectionTested(
                _logger,
                request.Address,
                request.Port,
                result.Outcome,
                result.FailedStage,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.Timeout,
                TestConnectionStage.TargetConnection,
                "Délai de connexion dépassé.");
        }
        catch (OperationCanceledException)
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.Cancelled,
                TestConnectionStage.None,
                "Test annulé.");
        }
        catch (CredentialException)
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.CredentialUnavailable,
                TestConnectionStage.CredentialLookup,
                "Identifiant introuvable dans le trousseau du système.");
        }
        catch (HostKeyVerificationException exception)
        {
            var outcome = exception.IsKeyChange
                ? TestConnectionOutcome.HostKeyChanged
                : TestConnectionOutcome.HostKeyUnknown;

            var result = new TestConnectionResultDto
            {
                Outcome = outcome,
                FailedStage = TestConnectionStage.HostKeyVerification,
                Message = exception.UserMessage,
                Elapsed = stopwatch.Elapsed,
                PresentedFingerprint = exception.PresentedFingerprint,
                KnownFingerprint = exception.KnownFingerprint,
            };

            ApplicationLog.ConnectionTested(
                _logger,
                request.Address,
                request.Port,
                result.Outcome,
                result.FailedStage,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (GatewayUnavailableException exception)
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.GatewayUnavailable,
                TestConnectionStage.GatewayConnection,
                exception.UserMessage);
        }
        catch (SshAuthenticationException exception)
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.AuthenticationFailed,
                TestConnectionStage.Authentication,
                exception.UserMessage);
        }
        catch (SshConnectionException exception)
        {
            var outcome = exception.Message.Contains("refused", StringComparison.OrdinalIgnoreCase)
                ? TestConnectionOutcome.ConnectionRefused
                : TestConnectionOutcome.Timeout;

            return Fail(
                request,
                stopwatch,
                outcome,
                TestConnectionStage.TargetConnection,
                exception.UserMessage);
        }
#pragma warning disable CA1031 // Le test de connexion doit toujours renvoyer un résultat typé (§30), jamais une exception brute.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Fail(
                request,
                stopwatch,
                TestConnectionOutcome.Unknown,
                TestConnectionStage.None,
                "Échec inattendu du test de connexion.");
        }
    }

    private TestConnectionResultDto Fail(
        TestConnectionRequestDto request,
        Stopwatch stopwatch,
        TestConnectionOutcome outcome,
        TestConnectionStage stage,
        string message)
    {
        var result = new TestConnectionResultDto
        {
            Outcome = outcome,
            FailedStage = stage,
            Message = message,
            Elapsed = stopwatch.Elapsed,
        };

        ApplicationLog.ConnectionTested(
            _logger,
            request.Address,
            request.Port,
            result.Outcome,
            result.FailedStage,
            stopwatch.ElapsedMilliseconds);

        return result;
    }

    private async Task TryDeleteAsync(CredentialReference credential)
    {
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _credentials.DeleteAsync(credential, cleanup.Token).ConfigureAwait(false);
        }
        catch (CredentialException)
        {
            // Nettoyage best-effort : le secret temporaire ne doit pas faire échouer le résultat.
        }
        catch (OperationCanceledException)
        {
        }
    }
}
