using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Linux;

namespace HostDeck.Infrastructure.Monitoring;

/// <summary>
/// État conservé d'un cycle à l'autre pour un serveur : indispensable au calcul du CPU et
/// des débits réseau, qui ne s'obtiennent que par différence de compteurs cumulés.
/// </summary>
internal sealed class ServerCollectionState
{
    public CpuTimes? PreviousCpu { get; set; }

    public MetricSample? PreviousSample { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? ConditionSince { get; set; }

    public AlertConditionKey? PendingCondition { get; set; }
}

/// <summary>Clé de condition d'alerte en cours de confirmation (durée).</summary>
internal readonly record struct AlertConditionKey(Guid RuleId, MonitoredMetric Metric);
