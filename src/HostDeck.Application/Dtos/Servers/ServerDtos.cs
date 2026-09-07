using System;
using System.Collections.Generic;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Dtos.Servers;

/// <summary>
/// Saisie de création d'un serveur.
///
/// Le secret lui-même transite ici une seule fois, à la création, pour être remis
/// immédiatement au trousseau du système : il n'est jamais persisté en base et n'est jamais
/// relu vers un DTO (§14, §30). C'est pourquoi <see cref="UpdateServerDto"/> ne le porte pas.
/// </summary>
public sealed record CreateServerDto
{
    public required string Name { get; init; }

    public required string Address { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    public CredentialKind CredentialKind { get; init; } = CredentialKind.PrivateKey;

    /// <summary>
    /// Clé privée ou mot de passe, transmis une seule fois vers le trousseau.
    /// Jamais journalisé, jamais écrit en base, jamais renvoyé par une lecture.
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>Passphrase de la clé privée, le cas échéant. Mêmes règles que le secret.</summary>
    public string? Passphrase { get; init; }

    public int MonitoringIntervalSeconds { get; init; } = 60;

    public bool DockerEnabled { get; init; }

    public string? Group { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public JumpHostDto? JumpHost { get; init; }
}

/// <summary>
/// Modification d'un serveur existant.
///
/// Ne porte volontairement aucun secret : changer un identifiant est une opération distincte,
/// pour qu'un formulaire d'édition ne puisse pas être pré-rempli avec une valeur sensible.
/// </summary>
public sealed record UpdateServerDto
{
    public required Guid ServerId { get; init; }

    public required string Name { get; init; }

    public required string Address { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    public int MonitoringIntervalSeconds { get; init; } = 60;

    public bool DockerEnabled { get; init; }

    public string? Group { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public JumpHostDto? JumpHost { get; init; }
}

/// <summary>Configuration d'un bastion.</summary>
public sealed record JumpHostDto
{
    public required string Address { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    /// <summary>
    /// Clé du credential du bastion dans le trousseau. Un bastion partagé par plusieurs
    /// cibles réutilise la même clé, ce qui évite de dupliquer le secret.
    /// </summary>
    public required string CredentialKey { get; init; }

    public CredentialKind CredentialKind { get; init; } = CredentialKind.PrivateKey;
}

/// <summary>
/// Ligne de la table Infrastructure : ce que l'inventaire affiche pour un hôte, sans
/// l'historique complet de ses métriques.
/// </summary>
public sealed record ServerSummaryDto
{
    public required Guid ServerId { get; init; }

    public required string Name { get; init; }

    public required string Address { get; init; }

    public required ServerStatus Status { get; init; }

    public ConnectionMode ConnectionMode { get; init; }

    public string? Group { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool DockerEnabled { get; init; }

    /// <summary>Système d'exploitation détecté, absent tant qu'aucune collecte n'a abouti.</summary>
    public string? OperatingSystem { get; init; }

    /// <summary>Dernières valeurs connues, absentes tant qu'aucune collecte n'a abouti.</summary>
    public LatestMetricDto? Latest { get; init; }

    public DateTimeOffset? LastCollectedAt { get; init; }
}

/// <summary>
/// Détail complet d'un hôte, pour l'écran de détail.
/// </summary>
public sealed record ServerDetailsDto
{
    public required ServerSummaryDto Summary { get; init; }

    public required ServerConfigurationDto Configuration { get; init; }

    public string? Hostname { get; init; }

    public string? KernelVersion { get; init; }

    public TimeSpan? Uptime { get; init; }

    /// <summary>Nombre d'incidents encore actifs sur cet hôte.</summary>
    public int ActiveIncidents { get; init; }
}

/// <summary>
/// Configuration éditable d'un hôte.
///
/// Porte la référence du credential, jamais le secret : un formulaire ne peut donc pas être
/// pré-rempli avec une valeur sensible relue depuis la base (§30).
/// </summary>
public sealed record ServerConfigurationDto
{
    public required Guid ServerId { get; init; }

    public required string Name { get; init; }

    public required string Address { get; init; }

    public required int Port { get; init; }

    public required string Username { get; init; }

    /// <summary>Clé opaque du credential dans le trousseau. Ne contient aucun secret.</summary>
    public required string CredentialKey { get; init; }

    public CredentialKind CredentialKind { get; init; }

    public int MonitoringIntervalSeconds { get; init; }

    public bool DockerEnabled { get; init; }

    public string? Group { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public JumpHostDto? JumpHost { get; init; }
}
