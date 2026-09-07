using System;

namespace HostDeck.Application.Errors;

/// <summary>
/// La connexion SSH n'a pas pu être établie : DNS, réseau, refus de connexion ou délai
/// dépassé.
/// </summary>
public class SshConnectionException : HostDeckException
{
    public SshConnectionException(string host, string message)
        : base($"SSH connection to '{host}' failed: {message}")
    {
        Host = host;
    }

    public SshConnectionException(string host, string message, Exception innerException)
        : base($"SSH connection to '{host}' failed: {message}", innerException)
    {
        Host = host;
    }

    public string Host { get; }

    public override string UserMessage => $"Connexion SSH impossible vers {Host}.";
}

/// <summary>
/// L'hôte a répondu mais a refusé les identifiants.
///
/// Distinguée de <see cref="SshConnectionException"/> : la remédiation n'est pas la même,
/// et un incident d'authentification ne doit pas être confondu avec une panne réseau.
/// </summary>
public sealed class SshAuthenticationException : SshConnectionException
{
    public SshAuthenticationException(string host, string username)
        : base(host, $"authentication rejected for user '{username}'")
    {
        Username = username;
    }

    public SshAuthenticationException(string host, string username, Exception innerException)
        : base(host, $"authentication rejected for user '{username}'", innerException)
    {
        Username = username;
    }

    public string Username { get; }

    public override string UserMessage =>
        $"Authentification refusée par {Host} pour l'utilisateur « {Username} ».";
}

/// <summary>
/// La clé d'hôte présentée n'est pas celle attendue.
///
/// C'est un événement de sécurité, pas une erreur de connexion ordinaire : une clé qui change
/// peut signaler une interception. Le type porte l'empreinte présentée et l'empreinte
/// approuvée pour que l'opérateur puisse trancher en connaissance de cause (§51, T3).
/// </summary>
public sealed class HostKeyVerificationException : HostDeckException
{
    public HostKeyVerificationException(
        string host,
        string presentedFingerprint,
        string? knownFingerprint)
        : base(BuildMessage(host, presentedFingerprint, knownFingerprint))
    {
        Host = host;
        PresentedFingerprint = presentedFingerprint;
        KnownFingerprint = knownFingerprint;
    }

    public string Host { get; }

    /// <summary>Empreinte que l'hôte vient de présenter.</summary>
    public string PresentedFingerprint { get; }

    /// <summary>Empreinte approuvée précédemment, ou <c>null</c> à la première connexion.</summary>
    public string? KnownFingerprint { get; }

    /// <summary>
    /// Vrai quand une empreinte différente était déjà approuvée. C'est le cas grave :
    /// une première connexion est seulement inconnue, une clé modifiée est suspecte.
    /// </summary>
    public bool IsKeyChange => KnownFingerprint is not null;

    public override string UserMessage => IsKeyChange
        ? $"La clé d'hôte de {Host} a changé. Cela peut indiquer une interception : vérifiez l'empreinte avant de continuer."
        : $"La clé d'hôte de {Host} est inconnue et doit être approuvée avant la première connexion.";

    private static string BuildMessage(string host, string presented, string? known) => known is null
        ? $"Host key for '{host}' is unknown (presented {presented})"
        : $"Host key for '{host}' changed (known {known}, presented {presented})";
}

/// <summary>
/// Le bastion est injoignable, donc la cible ne peut pas être atteinte.
///
/// Séparée pour que le moteur d'incidents puisse corréler : une passerelle tombée ne doit pas
/// produire un incident « hors ligne » indépendant par cible (§12).
/// </summary>
public sealed class GatewayUnavailableException : HostDeckException
{
    public GatewayUnavailableException(string gatewayHost, string targetHost, Exception innerException)
        : base($"Jump host '{gatewayHost}' is unreachable, target '{targetHost}' cannot be reached", innerException)
    {
        GatewayHost = gatewayHost;
        TargetHost = targetHost;
    }

    public string GatewayHost { get; }

    public string TargetHost { get; }

    public override string UserMessage =>
        $"Le bastion {GatewayHost} est injoignable : {TargetHost} ne peut pas être atteint.";
}

/// <summary>
/// Une commande distante a échoué ou renvoyé un code de sortie inattendu.
/// </summary>
public sealed class SshCommandException : HostDeckException
{
    public SshCommandException(string host, string command, int exitCode, string? standardError)
        : base($"Command '{command}' on '{host}' exited with code {exitCode}")
    {
        Host = host;
        Command = command;
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public string Host { get; }

    /// <summary>
    /// Commande exécutée. Sans danger dans un journal : HostDeck n'exécute que des commandes
    /// fixes et contrôlées, jamais assemblées depuis une saisie utilisateur (§51, T4).
    /// </summary>
    public string Command { get; }

    public int ExitCode { get; }

    public string? StandardError { get; }

    public override string UserMessage =>
        $"Une commande de collecte a échoué sur {Host} (code {ExitCode}).";
}
