using System;
using System.Collections.Generic;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;

namespace HostDeck.Application.Dtos.Incidents;

/// <summary>Ligne de la table des incidents actifs.</summary>
public sealed record IncidentDto
{
    public required Guid IncidentId { get; init; }

    public required Guid ServerId { get; init; }

    public required string ServerName { get; init; }

    public required Severity Severity { get; init; }

    public required IncidentStatus Status { get; init; }

    public required MonitoredMetric Metric { get; init; }

    /// <summary>Description lisible de la condition, par exemple « CPU élevée > 80 % ».</summary>
    public required string Problem { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset LastUpdatedAt { get; init; }

    /// <summary>Durée écoulée, figée une fois l'incident clos.</summary>
    public required TimeSpan Duration { get; init; }

    public string? AcknowledgedBy { get; init; }

    public DateTimeOffset? AcknowledgedAt { get; init; }

    public double? CurrentValue { get; init; }

    public double? ThresholdValue { get; init; }
}

/// <summary>
/// Détail d'un incident : ce qu'affiche le panneau de diagnostic, chronologie comprise.
/// </summary>
public sealed record IncidentDetailsDto
{
    public required IncidentDto Incident { get; init; }

    public required Guid RuleId { get; init; }

    public required string RuleName { get; init; }

    public string? RuleDescription { get; init; }

    public required IReadOnlyList<IncidentEventDto> Timeline { get; init; }

    public DateTimeOffset? RecoveredAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}

/// <summary>Entrée de la chronologie d'un incident.</summary>
public sealed record IncidentEventDto
{
    public required DateTimeOffset OccurredAt { get; init; }

    public required IncidentEventKind Kind { get; init; }

    /// <summary>Détail affiché sous le libellé, par exemple « Charge système = 2.12 ».</summary>
    public string? Detail { get; init; }

    /// <summary>Auteur de l'action, ou <c>null</c> quand c'est le système.</summary>
    public string? Actor { get; init; }
}

public enum IncidentEventKind
{
    ConditionDetected = 0,
    Opened = 1,
    ThresholdBreached = 2,
    Updated = 3,
    Escalated = 4,
    Acknowledged = 5,
    Recovered = 6,
    Resolved = 7,
    Notified = 8,
}

/// <summary>Acquittement d'un incident par un opérateur.</summary>
public sealed record AcknowledgeIncidentDto
{
    public required Guid IncidentId { get; init; }

    public required string AcknowledgedBy { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Critères de filtrage de la liste des incidents, alignés sur les filtres de la maquette.
/// </summary>
public sealed record IncidentFilterDto
{
    /// <summary>Sévérités retenues ; vide signifie « toutes ».</summary>
    public IReadOnlyList<Severity> Severities { get; init; } = [];

    /// <summary>Statuts retenus ; vide signifie « tous ».</summary>
    public IReadOnlyList<IncidentStatus> Statuses { get; init; } = [];

    public Guid? ServerId { get; init; }

    /// <summary>Recherche libre sur le nom d'hôte et le libellé du problème.</summary>
    public string? SearchText { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
