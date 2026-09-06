using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Ports;

/// <summary>
/// Accès au trousseau du système d'exploitation.
///
/// C'est le seul endroit où un secret existe. Il n'y a volontairement aucun repli en clair :
/// si le trousseau est indisponible, l'opération échoue avec
/// <see cref="Errors.CredentialException"/> plutôt que d'écrire le secret ailleurs (§51, T1).
/// </summary>
public interface ICredentialStore
{
    /// <summary>
    /// Lit un secret.
    ///
    /// Renvoie un <see cref="SecretMaterial"/> jetable plutôt qu'une <c>string</c> pour que
    /// l'appelant libère la mémoire dès qu'il a fini, et que le secret ne traîne pas dans le
    /// tas managé le temps d'un ramassage.
    /// </summary>
    /// <exception cref="Errors.CredentialException">Absent, trousseau indisponible ou accès refusé.</exception>
    Task<SecretMaterial> ReadAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        CredentialReference reference,
        ReadOnlyMemory<char> secret,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vrai si le trousseau du système répond. Permet à l'interface de prévenir avant qu'une
    /// collecte n'échoue en série.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Secret en mémoire, effacé à la libération.
///
/// Ne redéfinit ni <c>ToString</c> ni les membres d'égalité au-delà de ce qui est nécessaire,
/// pour qu'aucun chemin d'affichage accidentel — interpolation, journal, débogueur — ne
/// puisse en révéler le contenu (§51, T2).
/// </summary>
public sealed class SecretMaterial : IDisposable
{
    private char[]? _buffer;
    private int _length;

    public SecretMaterial(ReadOnlySpan<char> secret)
    {
        _buffer = new char[secret.Length];
        secret.CopyTo(_buffer);
        _length = secret.Length;
    }

    public int Length => _length;

    /// <summary>
    /// Expose le secret le temps d'un appel. Réservé aux adaptateurs qui doivent le remettre
    /// à une bibliothèque tierce.
    /// </summary>
    public ReadOnlySpan<char> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        return _buffer.AsSpan(0, _length);
    }

    public void Dispose()
    {
        if (_buffer is null)
        {
            return;
        }

        Array.Clear(_buffer);
        _buffer = null;
        _length = 0;
    }

    /// <summary>
    /// Ne révèle jamais le contenu, quel que soit le contexte d'appel.
    /// </summary>
    public override string ToString() => "«secret»";
}
