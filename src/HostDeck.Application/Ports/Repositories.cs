using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Ports;

/// <summary>
/// Persistance de l'inventaire des serveurs.
///
/// Renvoie des entités du domaine, pas des enregistrements de base : le mapping appartient
/// à l'Infrastructure (§8, §18).
/// </summary>
public interface IServerRepository
{
    Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Renvoie <c>null</c> si le serveur n'existe pas ; l'appelant décide quoi en faire.</summary>
    Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default);

    Task AddAsync(Server server, CancellationToken cancellationToken = default);

    Task UpdateAsync(Server server, CancellationToken cancellationToken = default);

    Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vrai si un autre serveur utilise déjà ce couple adresse/port, pour éviter les doublons
    /// silencieux dans l'inventaire.
    /// </summary>
    Task<bool> ExistsWithEndpointAsync(
        HostAddress address,
        Port port,
        ServerId? excluding = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistance des séries temporelles.
///
/// Le chemin chaud de cette interface est implémenté en SQL paramétré brut plutôt qu'avec le
/// suivi de changements d'EF Core : l'écriture par lots et l'agrégation en buckets portent sur
/// des volumes qu'un ORM matérialiserait inutilement (§18, §19).
/// </summary>
public interface IMetricsRepository
{
    /// <summary>
    /// Écrit un lot de relevés. Regroupé volontairement : une insertion par relevé
    /// multiplierait les transactions sur une base qui n'a qu'un seul écrivain.
    /// </summary>
    Task AddBatchAsync(
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken = default);

    Task<LatestMetricDto?> GetLatestAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renvoie un historique déjà agrégé au nombre de points demandé. L'agrégation a lieu
    /// côté SQL : charger la série brute pour la réduire en mémoire ne passerait pas
    /// l'échelle (§19).
    /// </summary>
    Task<MetricHistoryDto> GetHistoryAsync(
        MetricHistoryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricStatisticsDto>> GetStatisticsAsync(
        ServerId serverId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<MetricSeriesKind> series,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime les relevés antérieurs à la date de coupure et renvoie le nombre de lignes
    /// effacées.
    /// </summary>
    Task<int> PruneOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);
}

/// <summary>Persistance des incidents et de leur chronologie.</summary>
public interface IIncidentRepository
{
    Task<IReadOnlyList<Incident>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentDto>> QueryAsync(
        IncidentFilterDto filter,
        CancellationToken cancellationToken = default);

    Task<Incident?> FindAsync(IncidentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incident encore actif ouvert par cette règle sur ce serveur.
    ///
    /// C'est ce qui empêche d'ouvrir un nouvel incident à chaque cycle de collecte tant que la
    /// condition dure (§20).
    /// </summary>
    Task<Incident?> FindActiveAsync(
        ServerId serverId,
        AlertRuleId ruleId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Incident incident, CancellationToken cancellationToken = default);

    Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default);

    Task AppendEventAsync(
        IncidentId incidentId,
        IncidentEventDto incidentEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentEventDto>> GetTimelineAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default);

    Task<int> PruneResolvedOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);
}

/// <summary>Persistance des règles d'alerte.</summary>
public interface IAlertRuleRepository
{
    Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Règles actives, celles que le moteur d'évaluation doit appliquer.</summary>
    Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default);

    Task<AlertRule?> FindAsync(AlertRuleId id, CancellationToken cancellationToken = default);

    Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default);

    Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default);

    Task DeleteAsync(AlertRuleId id, CancellationToken cancellationToken = default);
}

/// <summary>Persistance des paramètres globaux.</summary>
public interface ISettingsRepository
{
    Task<SettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default);
}

/// <summary>
/// Paramètres tels qu'ils sont persistés.
///
/// Distinct de <see cref="Dtos.Settings.SettingsDto"/> : le DTO sert la Presentation et peut
/// évoluer pour l'affichage, cet enregistrement décrit ce qui est réellement stocké.
/// </summary>
public sealed record SettingsSnapshot
{
    public static readonly SettingsSnapshot Default = new();

    public string InstanceName { get; init; } = "HostDeck";

    public string? Description { get; init; }

    public int DefaultCollectionIntervalSeconds { get; init; } = 60;

    public int CollectionTimeoutSeconds { get; init; } = 10;

    public int CollectionRetryCount { get; init; } = 3;

    public int MaxConcurrentCollections { get; init; } = 8;

    public int MetricRetentionDays { get; init; } = 90;

    public int EventRetentionDays { get; init; } = 180;

    public bool DesktopNotificationsEnabled { get; init; } = true;

    public Severity MinimumNotificationSeverity { get; init; } = Severity.Average;

    public bool AutoRefreshEnabled { get; init; } = true;

    public int AutoRefreshIntervalSeconds { get; init; } = 30;

    public string? StorageDirectory { get; init; }

    public RetentionPolicy ToRetentionPolicy() =>
        RetentionPolicy.FromDays(MetricRetentionDays, EventRetentionDays);
}
