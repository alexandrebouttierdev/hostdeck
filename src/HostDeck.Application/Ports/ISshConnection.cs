using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Ports;

/// <summary>
/// Session SSH ouverte vers un hôte.
///
/// Le mode réel — direct ou à travers un bastion — n'apparaît pas dans cette interface : la
/// collecte ne doit pas avoir à le connaître (§12). L'objet <c>SshClient</c> sous-jacent ne
/// franchit jamais l'Infrastructure (§11).
/// </summary>
public interface ISshConnection : IAsyncDisposable
{
    /// <summary>Hôte réellement joint, pour le diagnostic et les journaux.</summary>
    string Host { get; }

    /// <summary>Latence mesurée à l'établissement de la session.</summary>
    TimeSpan Latency { get; }

    /// <summary>
    /// Exécute une commande.
    ///
    /// Les commandes sont des chaînes fixes et contrôlées, jamais assemblées depuis une
    /// saisie utilisateur : c'est la mesure qui exclut l'injection (§51, T4).
    /// </summary>
    Task<CommandResult> ExecuteAsync(
        string command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sortie d'une commande distante.
///
/// La sortie provient d'un hôte qui n'est pas de confiance : elle est traitée comme une
/// entrée potentiellement hostile et bornée en taille (§51, T6).
/// </summary>
public sealed record CommandResult
{
    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required int ExitCode { get; init; }

    public TimeSpan Duration { get; init; }

    /// <summary>Vrai si la sortie a été tronquée parce qu'elle dépassait la limite admise.</summary>
    public bool WasTruncated { get; init; }

    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Ouvre des sessions SSH, en masquant à l'appelant le fait qu'un bastion est traversé.
/// </summary>
public interface ISshConnectionFactory
{
    /// <summary>
    /// Établit une session vers un serveur, en traversant son bastion s'il en a un.
    /// </summary>
    /// <exception cref="Errors.GatewayUnavailableException">Le bastion est injoignable.</exception>
    /// <exception cref="Errors.SshAuthenticationException">Les identifiants sont refusés.</exception>
    /// <exception cref="Errors.HostKeyVerificationException">La clé d'hôte est inconnue ou a changé.</exception>
    Task<ISshConnection> ConnectAsync(
        Server server,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Empreintes de clés d'hôte approuvées.
///
/// Séparé du dépôt des serveurs parce que la décision qu'il porte est de nature différente :
/// approuver une clé est un acte de sécurité, pas une mise à jour de configuration (§51, T3).
/// </summary>
public interface IHostKeyStore
{
    /// <summary>Empreinte approuvée pour cet hôte, ou <c>null</c> si aucune ne l'est encore.</summary>
    Task<string?> FindApprovedFingerprintAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre une empreinte comme approuvée. Appelé uniquement après une décision
    /// explicite de l'opérateur, jamais automatiquement.
    /// </summary>
    Task ApproveAsync(
        string host,
        int port,
        string fingerprint,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(string host, int port, CancellationToken cancellationToken = default);
}
