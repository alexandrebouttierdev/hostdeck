// Adaptateur de frontière : les exceptions tierces (SSH.NET, Latchkey, OS) sont
// converties en erreurs Application typées ; CA1031 est donc désactivé ici.
#pragma warning disable CA1031
using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure;
using Latchkey;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Credentials;

/// <summary>
/// Adaptateur du trousseau OS via Latchkey (Credential Manager / Keychain / Secret Service).
///
/// Aucun repli en clair : si le backend natif est indisponible, l'opération échoue avec
/// <see cref="CredentialException"/> (§14, §51 T1). Aucun secret n'entre dans un journal.
/// </summary>
internal sealed class OsCredentialStore : ICredentialStore
{
    internal const string ServiceName = "dev.hostdeck.app";

    private readonly ILatchkey _store;
    private readonly ILogger<OsCredentialStore> _logger;

    public OsCredentialStore(ILatchkey store, ILogger<OsCredentialStore> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<SecretMaterial> ReadAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var value = await _store.GetAsync(reference.Key, cancellationToken).ConfigureAwait(false);
            if (value is null)
            {
                throw new CredentialException(
                    $"Credential '{reference.Key}' was not found in the OS store",
                    CredentialFailure.NotFound);
            }

            return new SecretMaterial(value.AsSpan());
        }
        catch (CredentialException)
        {
            throw;
        }
        catch (LatchkeyBackendUnavailableException exception)
        {
            InfrastructureLog.CredentialStoreUnavailable(_logger, exception);
            throw new CredentialException(
                "OS credential store is unavailable",
                CredentialFailure.StoreUnavailable,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new CredentialException(
                "Access to the OS credential store was denied",
                CredentialFailure.AccessDenied,
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new CredentialException(
                "Failed to read credential from the OS store",
                CredentialFailure.AccessDenied,
                exception);
        }
    }

    public async Task WriteAsync(
        CredentialReference reference,
        ReadOnlyMemory<char> secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _store.SetAsync(reference.Key, secret.ToString(), cancellationToken).ConfigureAwait(false);
            InfrastructureLog.CredentialWritten(_logger, reference.Key);
        }
        catch (LatchkeyBackendUnavailableException exception)
        {
            InfrastructureLog.CredentialStoreUnavailable(_logger, exception);
            throw new CredentialException(
                "OS credential store is unavailable",
                CredentialFailure.StoreUnavailable,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new CredentialException(
                "Access to the OS credential store was denied",
                CredentialFailure.AccessDenied,
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not CredentialException)
        {
            throw new CredentialException(
                "Failed to write credential to the OS store",
                CredentialFailure.WriteFailed,
                exception);
        }
    }

    public async Task DeleteAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _store.DeleteAsync(reference.Key, cancellationToken).ConfigureAwait(false);
            InfrastructureLog.CredentialDeleted(_logger, reference.Key);
        }
        catch (LatchkeyBackendUnavailableException exception)
        {
            InfrastructureLog.CredentialStoreUnavailable(_logger, exception);
            throw new CredentialException(
                "OS credential store is unavailable",
                CredentialFailure.StoreUnavailable,
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not CredentialException)
        {
            throw new CredentialException(
                "Failed to delete credential from the OS store",
                CredentialFailure.AccessDenied,
                exception);
        }
    }

    public async Task<bool> ExistsAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _store.ContainsAsync(reference.Key, cancellationToken).ConfigureAwait(false);
        }
        catch (LatchkeyBackendUnavailableException exception)
        {
            throw new CredentialException(
                "OS credential store is unavailable",
                CredentialFailure.StoreUnavailable,
                exception);
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Round-trip d'un jeton jetable : détecte un environnement sans Secret Service
            // avant qu'une collecte n'échoue en série.
            return Task.FromResult(Latchkey.Latchkey.VerifyPersistence(ServiceName));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            InfrastructureLog.CredentialStoreUnavailable(_logger, exception);
            return Task.FromResult(false);
        }
    }
}
