using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Incidents;

/// <summary>
/// Incident ouvert par le moteur d'alerte sur un serveur, pour une métrique donnée.
///
/// L'agrégat porte sa propre machine à états : toute transition invalide lève, plutôt que
/// d'écrire silencieusement un statut incohérent. Un incident résolu est terminal — il ne
/// peut ni être rouvert ni ré-acquitté. Un rebond de la condition ouvre un nouvel incident,
/// de sorte que l'historique reflète le nombre réel d'épisodes.
/// </summary>
public sealed class Incident
{
    public const int MaxAcknowledgedByLength = 128;

    private Incident(
        IncidentId id,
        ServerId serverId,
        AlertRuleId ruleId,
        MonitoredMetric metric,
        Severity severity,
        DateTimeOffset startedAt,
        double? triggeringValue,
        double? thresholdValue)
    {
        Id = id;
        ServerId = serverId;
        RuleId = ruleId;
        Metric = metric;
        Severity = severity;
        StartedAt = startedAt;
        LastUpdatedAt = startedAt;
        CurrentValue = triggeringValue;
        ThresholdValue = thresholdValue;
        Status = IncidentStatus.Open;
    }

    public IncidentId Id { get; }

    public ServerId ServerId { get; }

    public AlertRuleId RuleId { get; }

    public MonitoredMetric Metric { get; }

    public Severity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    /// <summary>Instant UTC d'ouverture.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Instant UTC de la dernière évolution, quelle qu'elle soit.</summary>
    public DateTimeOffset LastUpdatedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public string? AcknowledgedBy { get; private set; }

    /// <summary>Instant UTC où la condition a cessé, le cas échéant.</summary>
    public DateTimeOffset? RecoveredAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Dernière valeur observée pour la métrique surveillée.</summary>
    public double? CurrentValue { get; private set; }

    /// <summary>Valeur de seuil au moment du déclenchement, conservée pour l'historique.</summary>
    public double? ThresholdValue { get; }

    /// <summary>Instant UTC de la dernière notification envoyée, pour appliquer le cooldown.</summary>
    public DateTimeOffset? LastNotifiedAt { get; private set; }

    public bool IsActive => Status is IncidentStatus.Open or IncidentStatus.Acknowledged;

    /// <summary>
    /// Durée écoulée : figée à la résolution, courante tant que l'incident est ouvert.
    /// </summary>
    public TimeSpan DurationAt(DateTimeOffset now) =>
        (ResolvedAt ?? RecoveredAt ?? now.ToUniversalTime()) - StartedAt;

    /// <summary>
    /// Ouvre un incident à partir d'une règle dont la condition est confirmée.
    /// </summary>
    public static Incident Open(
        IncidentId id,
        ServerId serverId,
        AlertRule rule,
        DateTimeOffset startedAt,
        double? triggeringValue = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new Incident(
            id,
            serverId,
            rule.Id,
            rule.Metric,
            rule.Severity,
            Guard.RequiredInstant(startedAt).ToUniversalTime(),
            triggeringValue,
            rule.Threshold?.Value);
    }

    /// <summary>
    /// Enregistre une nouvelle observation alors que la condition est toujours remplie.
    /// N'altère pas le statut : un incident acquitté reste acquitté tant qu'il dure.
    /// </summary>
    public void RecordObservation(double? observedValue, DateTimeOffset observedAt)
    {
        EnsureNotResolved(nameof(RecordObservation));

        var instant = Normalize(observedAt);

        CurrentValue = observedValue;
        LastUpdatedAt = instant;

        // La condition est de nouveau remplie après un rétablissement : l'incident redevient
        // actif au lieu de rester faussement rétabli.
        if (Status == IncidentStatus.Recovered)
        {
            Status = IncidentStatus.Open;
            RecoveredAt = null;
        }
    }

    /// <summary>
    /// Acquitte l'incident : un opérateur déclare l'avoir pris en charge.
    /// </summary>
    public void Acknowledge(string acknowledgedBy, DateTimeOffset acknowledgedAt)
    {
        EnsureNotResolved(nameof(Acknowledge));

        if (Status == IncidentStatus.Acknowledged)
        {
            throw new DomainValidationException(
                nameof(Status),
                "l'incident est déjà acquitté");
        }

        var instant = Normalize(acknowledgedAt);

        AcknowledgedBy = Guard.RequiredText(acknowledgedBy, MaxAcknowledgedByLength);
        AcknowledgedAt = instant;
        LastUpdatedAt = instant;
        Status = IncidentStatus.Acknowledged;
    }

    /// <summary>
    /// Constate que la condition a cessé. L'incident n'est pas clos pour autant : un
    /// incident non acquitté qui se rétablit seul doit rester visible.
    /// </summary>
    public void Recover(DateTimeOffset recoveredAt)
    {
        EnsureNotResolved(nameof(Recover));

        if (Status == IncidentStatus.Recovered)
        {
            return;
        }

        var instant = Normalize(recoveredAt);

        RecoveredAt = instant;
        LastUpdatedAt = instant;
        Status = IncidentStatus.Recovered;
    }

    /// <summary>
    /// Clôt l'incident. État terminal.
    /// </summary>
    public void Resolve(DateTimeOffset resolvedAt)
    {
        EnsureNotResolved(nameof(Resolve));

        var instant = Normalize(resolvedAt);

        ResolvedAt = instant;
        LastUpdatedAt = instant;
        Status = IncidentStatus.Resolved;
    }

    /// <summary>
    /// Élève la gravité d'un incident en cours quand la situation s'aggrave.
    /// La gravité ne redescend jamais : abaisser la gravité d'un incident déjà notifié
    /// masquerait l'épisode réel dans l'historique.
    /// </summary>
    public void Escalate(Severity severity, DateTimeOffset observedAt)
    {
        EnsureNotResolved(nameof(Escalate));

        if (severity <= Severity)
        {
            return;
        }

        Severity = severity;
        LastUpdatedAt = Normalize(observedAt);
    }

    /// <summary>
    /// Marque qu'une notification vient de partir, ce qui arme le cooldown de la règle.
    /// </summary>
    public void MarkNotified(DateTimeOffset notifiedAt) =>
        LastNotifiedAt = Normalize(notifiedAt);

    private void EnsureNotResolved(string operation)
    {
        if (Status == IncidentStatus.Resolved)
        {
            throw new DomainValidationException(
                nameof(Status),
                $"{operation} est impossible sur un incident résolu");
        }
    }

    /// <summary>
    /// Ramène l'instant en UTC et refuse un événement antérieur au dernier connu : une
    /// chronologie d'incident qui recule serait illisible et fausserait les durées.
    /// </summary>
    private DateTimeOffset Normalize(DateTimeOffset instant)
    {
        var utc = Guard.RequiredInstant(instant).ToUniversalTime();
        Guard.NotBefore(utc, LastUpdatedAt, nameof(instant), nameof(LastUpdatedAt));
        return utc;
    }
}
