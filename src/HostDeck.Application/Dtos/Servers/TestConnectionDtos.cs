using System;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Dtos.Servers;

/// <summary>
/// Demande de test de connexion.
///
/// Prend une configuration complète plutôt qu'un identifiant : l'opérateur doit pouvoir
/// tester une configuration en cours de saisie, avant même de l'enregistrer.
/// </summary>
public sealed record TestConnectionRequestDto
{
    public required string Address { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    /// <summary>Credential déjà présent dans le trousseau.</summary>
    public required string CredentialKey { get; init; }

    public CredentialKind CredentialKind { get; init; } = CredentialKind.PrivateKey;

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

    /// <summary>Le nom n'a pas pu être résolu.</summary>
    DnsResolutionFailed = 1,

    /// <summary>Aucune réponse dans le délai imparti.</summary>
    Timeout = 2,

    /// <summary>La connexion TCP a été refusée.</summary>
    ConnectionRefused = 3,

    /// <summary>Les identifiants ont été rejetés.</summary>
    AuthenticationFailed = 4,

    /// <summary>La clé d'hôte est inconnue et doit être approuvée.</summary>
    HostKeyUnknown = 5,

    /// <summary>La clé d'hôte a changé : événement de sécurité.</summary>
    HostKeyChanged = 6,

    /// <summary>Le bastion n'a pas pu être joint ; la cible n'a donc pas été testée.</summary>
    GatewayUnavailable = 7,

    /// <summary>Le bastion a répondu mais la cible reste injoignable à travers lui.</summary>
    TargetUnreachableThroughGateway = 8,

    /// <summary>Le credential est absent ou illisible dans le trousseau.</summary>
    CredentialUnavailable = 9,

    /// <summary>SSH fonctionne, mais le moteur Docker est inaccessible.</summary>
    DockerUnavailable = 10,

    /// <summary>L'opérateur a annulé le test.</summary>
    Cancelled = 11,

    /// <summary>Échec inattendu ; le détail technique est journalisé, pas affiché.</summary>
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
