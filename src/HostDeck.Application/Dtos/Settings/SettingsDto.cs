namespace HostDeck.Application.Dtos.Settings;

/// <summary>
/// Paramètres globaux de l'application, tels que les présente l'écran Paramètres.
///
/// Ne contient aucun secret : les identifiants de serveur vivent dans le trousseau du
/// système et les jetons de canaux externes suivront la même règle (§14).
/// </summary>
public sealed record SettingsDto
{
    public string InstanceName { get; init; } = "HostDeck";

    public string? Description { get; init; }

    /// <summary>Intervalle de collecte appliqué aux hôtes qui n'en définissent pas.</summary>
    public int DefaultCollectionIntervalSeconds { get; init; } = 60;

    /// <summary>Délai au-delà duquel une collecte est abandonnée.</summary>
    public int CollectionTimeoutSeconds { get; init; } = 10;

    /// <summary>Nombre de tentatives avant de déclarer un hôte injoignable.</summary>
    public int CollectionRetryCount { get; init; } = 3;

    /// <summary>
    /// Nombre d'hôtes collectés simultanément. Borné : c'est ce qui empêche HostDeck de
    /// devenir une source de charge sur la production qu'il supervise (§51, T8).
    /// </summary>
    public int MaxConcurrentCollections { get; init; } = 8;

    public int MetricRetentionDays { get; init; } = 90;

    public int EventRetentionDays { get; init; } = 180;

    public bool DesktopNotificationsEnabled { get; init; } = true;

    /// <summary>Sévérité minimale déclenchant une notification bureau.</summary>
    public Domain.Incidents.Severity MinimumNotificationSeverity { get; init; } =
        Domain.Incidents.Severity.Average;

    public bool AutoRefreshEnabled { get; init; } = true;

    public int AutoRefreshIntervalSeconds { get; init; } = 30;

    /// <summary>Emplacement de la base locale. Vide signifie « répertoire par défaut ».</summary>
    public string? StorageDirectory { get; init; }
}
