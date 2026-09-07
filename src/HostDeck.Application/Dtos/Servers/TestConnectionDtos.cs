using System;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Dtos.Servers;

/// <summary>
/// Demande de test de connexion.
///
/// Prend une configuration complète plutôt qu'un identifiant : l'opérateur doit pouvoir
/// tester une configuration en cours de saisie, avant même de l'enregistrer.
/// Soit <see cref="CredentialKey"/> (secret déjà en trousseau), soit <see cref="Secret"/>
/// (saisie en cours) doit être fourni — jamais les deux en conflit de priorité : le secret
/// inline l'emporte pour le parcours « ajouter un hôte ».
/// </summary>
public sealed record TestConnectionRequestDto
{
    public required string Address { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    /// <summary>Credential déjà présent dans le trousseau. Optionnel si <see cref="Secret"/> est fourni.</summary>
    public string? CredentialKey { get; init; }

    public CredentialKind CredentialKind { get; init; } = CredentialKind.PrivateKey;

    /// <summary>
    /// Secret saisi dans le formulaire, avant enregistrement. Remis temporairement au
    /// trousseau le temps du test, puis retiré. Jamais journalisé ni persisté en base.
    /// </summary>
    public string? Secret { get; init; }

    /// <summary>Passphrase de la clé privée, le cas échéant. Mêmes règles que le secret.</summary>
    public string? Passphrase { get; init; }

    public JumpHostDto? JumpHost { get; init; }

    /// <summary>Teste aussi l'accès au moteur Docker après la connexion SSH.</summary>
    public bool TestDocker { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}

/// <summary>
/// Issue d'un test de connexion.
///
/// Le §30 impose de distinguer les causes : DNS, délai, authentification, clé d'hôte,
/// bastion, cible, Docker. Un simple booléen obligerait l'opérateur à deviner, et un
/// échec de bastion serait confondu avec une cible en panne. C'est pourquoi ce type est un
/// résultat et non une exception : chacune de ces issues est attendue (§38).
/// </summary>
public sealed record TestConnectionResultDto
{
    public required TestConnectionOutcome Outcome { get; init; }

    /// <summary>Étape à laquelle le test s'est arrêté.</summary>
    public required TestConnectionStage FailedStage { get; init; }

    /// <summary>Message en français destiné à l'opérateur, sans détail technique.</summary>
    public required string Message { get; init; }

    /// <summary>Durée totale du test, affichée à côté du résultat.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Latence SSH mesurée quand la connexion a abouti.</summary>
    public TimeSpan? SshLatency { get; init; }

    /// <summary>
    /// Empreinte présentée par l'hôte, renseignée quand la clé est inconnue ou a changé,
    /// pour que l'opérateur puisse la vérifier avant d'approuver (§51, T3).
    /// </summary>
    public string? PresentedFingerprint { get; init; }

    /// <summary>Empreinte déjà approuvée, renseignée uniquement si elle diffère.</summary>
    public string? KnownFingerprint { get; init; }

    /// <summary>Système détecté quand la connexion a abouti.</summary>
    public string? DetectedOperatingSystem { get; init; }

    /// <summary>Version du moteur Docker quand son test a été demandé et a abouti.</summary>
    public string? DockerVersion { get; init; }

    public bool IsSuccess => Outcome == TestConnectionOutcome.Success;

    public static TestConnectionResultDto Success(
        TimeSpan elapsed,
        TimeSpan sshLatency,
        string? operatingSystem = null,
        string? dockerVersion = null) => new()
        {
            Outcome = TestConnectionOutcome.Success,
            FailedStage = TestConnectionStage.None,
            Message = "Connexion réussie.",
            Elapsed = elapsed,
            SshLatency = sshLatency,
            DetectedOperatingSystem = operatingSystem,
            DockerVersion = dockerVersion,
        };
}

/// <summary>Cause précise de l'issue d'un test de connexion.</summary>
public enum TestConnectionOutcome
{
    Success = 0,
    DnsResolutionFailed = 1,
    Timeout = 2,
    ConnectionRefused = 3,
    AuthenticationFailed = 4,
    HostKeyUnknown = 5,
    HostKeyChanged = 6,
    GatewayUnavailable = 7,
    TargetUnreachableThroughGateway = 8,
    CredentialUnavailable = 9,
    DockerUnavailable = 10,
    Cancelled = 11,
    Unknown = 12,
}

/// <summary>Étape du test à laquelle l'échec est survenu.</summary>
public enum TestConnectionStage
{
    None = 0,
    CredentialLookup = 1,
    GatewayConnection = 2,
    TargetConnection = 3,
    HostKeyVerification = 4,
    Authentication = 5,
    CommandExecution = 6,
    DockerProbe = 7,
}
