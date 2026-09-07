using System;
using System.Collections.Generic;

namespace HostDeck.Infrastructure.Persistence.Records;

/// <summary>
/// Ligne de la table <c>servers</c>.
///
/// Type distinct de l'entité <see cref="Domain.Servers.Server"/> (§18) : celle-ci protège ses
/// invariants par son constructeur et n'expose que des setters privés, ce qu'EF Core devrait
/// contourner pour la matérialiser. Séparer les deux garde le domaine libre de toute
/// contrainte de mapping, et permet de faire évoluer le schéma sans toucher au modèle métier.
///
/// Ne contient aucun secret : seule la <em>clé</em> du credential dans le trousseau est
/// stockée (§14).
/// </summary>
internal sealed class ServerRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Clé opaque dans le trousseau du système. Jamais le secret lui-même.</summary>
    public string CredentialKey { get; set; } = string.Empty;

    public int CredentialKind { get; set; }

    public int MonitoringIntervalSeconds { get; set; }

    public bool DockerEnabled { get; set; }

    public string? GroupName { get; set; }

    public int Status { get; set; }

    public DateTimeOffset? LastCollectedAt { get; set; }

    /// <summary>Hostname observé sur l'hôte. Null tant qu'aucune collecte n'a abouti.</summary>
    public string? Hostname { get; set; }

    /// <summary>Distribution détectée (os-release). Null tant qu'aucune collecte n'a abouti.</summary>
    public string? OperatingSystem { get; set; }

    /// <summary>Version du noyau observée. Null tant qu'aucune collecte n'a abouti.</summary>
    public string? KernelVersion { get; set; }

    // Bastion : colonnes en ligne plutôt qu'une table dédiée. Un serveur a au plus un
    // bastion, et une jointure sur chaque lecture d'inventaire ne se justifierait pas.
    public string? JumpHostAddress { get; set; }

    public int? JumpHostPort { get; set; }

    public string? JumpHostUsername { get; set; }

    public string? JumpHostCredentialKey { get; set; }

    public int? JumpHostCredentialKind { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ServerTagRecord> Tags { get; } = [];
}

/// <summary>Ligne de la table <c>server_tags</c>.</summary>
internal sealed class ServerTagRecord
{
    public Guid ServerId { get; set; }

    public string Tag { get; set; } = string.Empty;
}
