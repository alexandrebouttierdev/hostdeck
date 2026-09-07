// Adaptateur de frontière : les exceptions tierces (SSH.NET, Latchkey, OS) sont
// converties en erreurs Application typées ; CA1031 est donc désactivé ici.
#pragma warning disable CA1031
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Ouvre des sessions SSH directes ou via bastion. Le mode réel reste invisible pour la
/// collecte (§12).
/// </summary>
internal sealed class SshConnectionFactory : ISshConnectionFactory
{
    private readonly ICredentialStore _credentials;
    private readonly IHostKeyStore _hostKeys;
    private readonly SshCredentialLoader _credentialLoader;
    private readonly SshConnectionOptions _options;
    private readonly ILogger<SshConnectionFactory> _logger;

    public SshConnectionFactory(
        ICredentialStore credentials,
        IHostKeyStore hostKeys,
        SshCredentialLoader credentialLoader,
        IOptions<SshConnectionOptions> options,
        ILogger<SshConnectionFactory> logger)
    {
        _credentials = credentials;
        _hostKeys = hostKeys;
        _credentialLoader = credentialLoader;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ISshConnection> ConnectAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        cancellationToken.ThrowIfCancellationRequested();

        if (server.JumpHost is { } jumpHost)
        {
            return await ConnectViaJumpHostAsync(server, jumpHost, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ConnectDirectAsync(server, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ISshConnection> ConnectDirectAsync(
        Server server,
        CancellationToken cancellationToken)
    {
        var host = server.Address.Value;
        var port = server.Port.Value;
        var username = server.Username.Value;

        AuthenticationMethod authentication;
        try
        {
            authentication = await _credentialLoader
                .CreateAuthenticationAsync(server.Username, server.Credential, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CredentialException)
        {
            throw;
        }

        var connectionInfo = new ConnectionInfo(host, port, username, authentication)
        {
            Timeout = _options.ConnectTimeout,
        };

        var client = new SshClient(connectionInfo)
        {
            KeepAliveInterval = _options.KeepAliveInterval,
        };

        var gate = new SshHostKeyGate();
        var known = await _hostKeys
            .FindApprovedFingerprintAsync(host, port, cancellationToken)
            .ConfigureAwait(false);
        gate.Attach(client, host, port, known);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            gate.ThrowIfRejected();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            client.Dispose();
            gate.ThrowIfRejected();
            throw SshExceptionMapper.MapConnectionFailure(host, username, exception);
        }

        InfrastructureLog.SshConnected(_logger, host, port, stopwatch.ElapsedMilliseconds, viaJump: false);
        return new SshConnection(client, host, stopwatch.Elapsed, _options);
    }

    private async Task<ISshConnection> ConnectViaJumpHostAsync(
        Server server,
        JumpHost jumpHost,
        CancellationToken cancellationToken)
    {
        var jumpAddress = jumpHost.Address.Value;
        var jumpPort = jumpHost.Port.Value;
        var targetAddress = server.Address.Value;
        var targetPort = server.Port.Value;

        AuthenticationMethod jumpAuth;
        try
        {
            jumpAuth = await _credentialLoader
                .CreateAuthenticationAsync(jumpHost.Username, jumpHost.Credential, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CredentialException exception)
        {
            throw new GatewayUnavailableException(jumpAddress, targetAddress, exception);
        }

        var jumpInfo = new ConnectionInfo(jumpAddress, jumpPort, jumpHost.Username.Value, jumpAuth)
        {
            Timeout = _options.ConnectTimeout,
        };

        var jumpClient = new SshClient(jumpInfo)
        {
            KeepAliveInterval = _options.KeepAliveInterval,
        };

        var jumpGate = new SshHostKeyGate();
        var knownJump = await _hostKeys
            .FindApprovedFingerprintAsync(jumpAddress, jumpPort, cancellationToken)
            .ConfigureAwait(false);
        jumpGate.Attach(jumpClient, jumpAddress, jumpPort, knownJump);

        try
        {
            await jumpClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            jumpGate.ThrowIfRejected();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            jumpClient.Dispose();
            jumpGate.ThrowIfRejected();
            throw SshExceptionMapper.MapConnectionFailure(
                jumpAddress,
                jumpHost.Username.Value,
                exception,
                isJumpHost: true,
                targetHost: targetAddress);
        }

        ForwardedPortLocal? forwarded = null;
        SshClient? targetClient = null;

        try
        {
            // Port local éphémère (0) : le système choisit, on lit BoundPort ensuite.
            forwarded = new ForwardedPortLocal("127.0.0.1", 0, targetAddress, (uint)targetPort);
            jumpClient.AddForwardedPort(forwarded);
            forwarded.Start();

            AuthenticationMethod targetAuth;
            try
            {
                targetAuth = await _credentialLoader
                    .CreateAuthenticationAsync(server.Username, server.Credential, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (CredentialException)
            {
                throw;
            }

            var targetInfo = new ConnectionInfo(
                "127.0.0.1",
                (int)forwarded.BoundPort,
                server.Username.Value,
                targetAuth)
            {
                Timeout = _options.ConnectTimeout,
            };

            targetClient = new SshClient(targetInfo)
            {
                KeepAliveInterval = _options.KeepAliveInterval,
            };

            // La clé d'hôte est celle de la cible logique, pas de 127.0.0.1.
            var targetGate = new SshHostKeyGate();
            var knownTarget = await _hostKeys
                .FindApprovedFingerprintAsync(targetAddress, targetPort, cancellationToken)
                .ConfigureAwait(false);
            targetGate.Attach(targetClient, targetAddress, targetPort, knownTarget);

            var stopwatch = Stopwatch.StartNew();
            await targetClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            targetGate.ThrowIfRejected();

            InfrastructureLog.SshConnected(
                _logger,
                targetAddress,
                targetPort,
                stopwatch.ElapsedMilliseconds,
                viaJump: true);

            return new SshConnection(
                targetClient,
                targetAddress,
                stopwatch.Elapsed,
                _options,
                jumpClient,
                forwarded);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
                                          and not HostKeyVerificationException
                                          and not CredentialException
                                          and not Application.Errors.SshAuthenticationException)
        {
            targetClient?.Dispose();
            try
            {
                if (forwarded is { IsStarted: true })
                {
                    forwarded.Stop();
                }
            }
            catch
            {
                // ignore
            }

            forwarded?.Dispose();
            jumpClient.Dispose();

            if (exception is HostKeyVerificationException)
            {
                throw;
            }

            throw new Application.Errors.SshConnectionException(
                targetAddress,
                exception.GetType().Name,
                exception);
        }
        catch
        {
            targetClient?.Dispose();
            try
            {
                if (forwarded is { IsStarted: true })
                {
                    forwarded.Stop();
                }
            }
            catch
            {
                // ignore
            }

            forwarded?.Dispose();
            jumpClient.Dispose();
            throw;
        }
    }
}
