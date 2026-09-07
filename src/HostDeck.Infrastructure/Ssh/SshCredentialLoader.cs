// Adaptateur de frontière : les exceptions tierces (SSH.NET, Latchkey, OS) sont
// converties en erreurs Application typées ; CA1031 est donc désactivé ici.
#pragma warning disable CA1031
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using Renci.SshNet;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Construit les méthodes d'authentification SSH.NET à partir du trousseau, sans jamais
/// journaliser le secret.
/// </summary>
internal sealed class SshCredentialLoader
{
    private readonly ICredentialStore _credentials;

    public SshCredentialLoader(ICredentialStore credentials)
    {
        _credentials = credentials;
    }

    public async Task<AuthenticationMethod> CreateAuthenticationAsync(
        SshUsername username,
        CredentialReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(reference);

        using var secret = await _credentials.ReadAsync(reference, cancellationToken).ConfigureAwait(false);

        return reference.Kind switch
        {
            CredentialKind.Password => CreatePasswordMethod(username.Value, secret),
            CredentialKind.PrivateKey => await CreatePrivateKeyMethodAsync(
                username.Value,
                reference,
                secret,
                cancellationToken).ConfigureAwait(false),
            _ => throw new CredentialException(
                $"Unsupported credential kind '{reference.Kind}' for '{reference.Key}'",
                CredentialFailure.NotFound),
        };
    }

    private static PasswordAuthenticationMethod CreatePasswordMethod(
        string username,
        SecretMaterial secret)
    {
        // SSH.NET exige une string : le client en conserve une copie pour la durée de session.
#pragma warning disable CA2000 // Ownership transferrée au ConnectionInfo / SshClient.
        return new PasswordAuthenticationMethod(username, secret.AsSpan().ToString());
#pragma warning restore CA2000
    }

    private async Task<PrivateKeyAuthenticationMethod> CreatePrivateKeyMethodAsync(
        string username,
        CredentialReference reference,
        SecretMaterial secret,
        CancellationToken cancellationToken)
    {
        string? passphrase = null;
        var passphraseReference = new CredentialReference($"{reference.Key}:passphrase", reference.Kind);

        if (await _credentials.ExistsAsync(passphraseReference, cancellationToken).ConfigureAwait(false))
        {
            using var passphraseMaterial = await _credentials
                .ReadAsync(passphraseReference, cancellationToken)
                .ConfigureAwait(false);
            passphrase = passphraseMaterial.AsSpan().ToString();
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(secret.AsSpan().ToString());
            try
            {
                await using var stream = new MemoryStream(bytes, writable: false);
                var keyFile = string.IsNullOrEmpty(passphrase)
                    ? new PrivateKeyFile(stream)
                    : new PrivateKeyFile(stream, passphrase);
#pragma warning disable CA2000 // Ownership transferrée au ConnectionInfo / SshClient.
                return new PrivateKeyAuthenticationMethod(username, keyFile);
#pragma warning restore CA2000
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SshAuthenticationException(
                reference.Key,
                username,
                exception);
        }
    }
}
