using System;

namespace HostDeck.Infrastructure.Persistence.Records;

/// <summary>Ligne de la table <c>incidents</c>.</summary>
internal sealed class IncidentRecord
{
    public Guid Id { get; set; }

    public Guid ServerId { get; set; }

    public Guid RuleId { get; set; }

    public int Metric { get; set; }

    public int Severity { get; set; }

    public int Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset LastUpdatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? RecoveredAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public double? CurrentValue { get; set; }

    public double? ThresholdValue { get; set; }

    public DateTimeOffset? LastNotifiedAt { get; set; }
}

/// <summary>
/// Ligne de la table <c>incident_events</c>, qui porte la chronologie d'un incident.
///
/// Table séparée plutôt que colonne sérialisée : la chronologie s'écrit par ajout, se lit
/// triée, et se purge indépendamment de l'incident lui-même.
/// </summary>
internal sealed class IncidentEventRecord
{
    public long Id { get; set; }

    public Guid IncidentId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public int Kind { get; set; }

    public string? Detail { get; set; }

    public string? Actor { get; set; }
}
