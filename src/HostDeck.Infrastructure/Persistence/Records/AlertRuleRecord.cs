using System;

namespace HostDeck.Infrastructure.Persistence.Records;

/// <summary>Ligne de la table <c>alert_rules</c>.</summary>
internal sealed class AlertRuleRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Metric { get; set; }

    public int Severity { get; set; }

    /// <summary>Absents pour une métrique d'état binaire, qui n'admet pas de seuil.</summary>
    public int? ComparisonOperator { get; set; }

    public double? ThresholdValue { get; set; }

    public int DurationSeconds { get; set; }

    public int CooldownSeconds { get; set; }

    public int ScopeKind { get; set; }

    public string? ScopeGroup { get; set; }

    public Guid? ScopeServerId { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Ligne unique de la table <c>settings</c>.
///
/// Une seule ligne, verrouillée par une contrainte sur la clé primaire : les paramètres sont
/// globaux à l'installation, et permettre plusieurs lignes créerait une ambiguïté silencieuse
/// sur celle qui fait foi.
/// </summary>
internal sealed class SettingsRecord
{
    /// <summary>Toujours 1. Contraint à cette valeur par la configuration EF.</summary>
    public int Id { get; set; } = 1;

    public string InstanceName { get; set; } = "HostDeck";

    public string? Description { get; set; }

    public int DefaultCollectionIntervalSeconds { get; set; } = 60;

    public int CollectionTimeoutSeconds { get; set; } = 10;

    public int CollectionRetryCount { get; set; } = 3;

    public int MaxConcurrentCollections { get; set; } = 8;

    public int MetricRetentionDays { get; set; } = 90;

    public int EventRetentionDays { get; set; } = 180;

    public bool DesktopNotificationsEnabled { get; set; } = true;

    public int MinimumNotificationSeverity { get; set; } = 2;

    public bool AutoRefreshEnabled { get; set; } = true;

    public int AutoRefreshIntervalSeconds { get; set; } = 30;

    public string? StorageDirectory { get; set; }
}

/// <summary>
/// Ligne de la table <c>ssh_host_keys</c> : une empreinte de clé d'hôte approuvée.
///
/// Table distincte de <c>servers</c> parce que la donnée est de nature différente : une
/// empreinte est approuvée pour un couple hôte/port, indépendamment du nombre de serveurs
/// configurés qui l'utilisent, et son écriture est une décision de sécurité (§51, T3).
/// </summary>
internal sealed class SshHostKeyRecord
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public DateTimeOffset ApprovedAt { get; set; }
}
