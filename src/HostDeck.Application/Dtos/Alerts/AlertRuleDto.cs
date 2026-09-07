using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;

namespace HostDeck.Application.Dtos.Alerts;

/// <summary>Règle d'alerte telle que l'écran des règles la présente et l'édite.</summary>
public sealed record AlertRuleDto
{
    public required Guid RuleId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required MonitoredMetric Metric { get; init; }

    public required Severity Severity { get; init; }

    /// <summary>Absents pour une métrique d'état binaire, qui n'a pas de seuil.</summary>
    public ComparisonOperator? Comparison { get; init; }

    public double? ThresholdValue { get; init; }

    /// <summary>Durée pendant laquelle la condition doit tenir avant de déclencher.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Délai minimal entre deux notifications d'un même incident.</summary>
    public TimeSpan Cooldown { get; init; }

    public required AlertScopeKind ScopeKind { get; init; }

    public string? ScopeGroup { get; init; }

    public Guid? ScopeServerId { get; init; }

    public bool IsEnabled { get; init; }

    /// <summary>Dernière évaluation, informative ; absente tant que la règle n'a pas tourné.</summary>
    public DateTimeOffset? LastEvaluatedAt { get; init; }

    /// <summary>Nombre d'incidents actuellement ouverts par cette règle.</summary>
    public int ActiveIncidents { get; init; }
}

/// <summary>Saisie de création d'une règle d'alerte.</summary>
public sealed record CreateAlertRuleDto
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required MonitoredMetric Metric { get; init; }

    public required Severity Severity { get; init; }

    public ComparisonOperator? Comparison { get; init; }

    public double? ThresholdValue { get; init; }

    public TimeSpan Duration { get; init; }

    public TimeSpan Cooldown { get; init; }

    public AlertScopeKind ScopeKind { get; init; } = AlertScopeKind.Global;

    public string? ScopeGroup { get; init; }

    public Guid? ScopeServerId { get; init; }

    public bool IsEnabled { get; init; } = true;
}

/// <summary>Modification d'une règle existante.</summary>
public sealed record UpdateAlertRuleDto
{
    public required Guid RuleId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required Severity Severity { get; init; }

    public ComparisonOperator? Comparison { get; init; }

    public double? ThresholdValue { get; init; }

    public TimeSpan Duration { get; init; }

    public TimeSpan Cooldown { get; init; }

    public AlertScopeKind ScopeKind { get; init; } = AlertScopeKind.Global;

    public string? ScopeGroup { get; init; }

    public Guid? ScopeServerId { get; init; }

    public bool IsEnabled { get; init; } = true;
}
