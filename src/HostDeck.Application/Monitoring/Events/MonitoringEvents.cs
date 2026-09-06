using System;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Monitoring.Events;

/// <summary>
/// Changement produit par la supervision.
///
/// Ce bus existe pour découpler le coordinateur de collecte des ViewModels : sans lui, un
/// service de fond garderait des références sur des vues et les retiendrait après leur
/// fermeture (§64). Aucun événement ne transporte de secret.
/// </summary>
public abstract record MonitoringEvent
{
    protected MonitoringEvent(DateTimeOffset occurredAt)
    {
        OccurredAt = occurredAt;
    }

    public DateTimeOffset OccurredAt { get; }
}

/// <summary>De nouvelles valeurs sont disponibles pour un hôte.</summary>
public sealed record MetricUpdatedEvent : MonitoringEvent
{
    public MetricUpdatedEvent(ServerId serverId, LatestMetricDto metric, DateTimeOffset occurredAt)
        : base(occurredAt)
    {
        ServerId = serverId;
        Metric = metric;
    }

    public ServerId ServerId { get; }

    public LatestMetricDto Metric { get; }
}

/// <summary>
/// Le statut d'un hôte a changé.
///
/// Émis uniquement sur un changement réel, jamais à chaque cycle : republier « en ligne »
/// toutes les 60 secondes ferait retracer l'inventaire entier sans raison (§37).
/// </summary>
public sealed record ServerStatusChangedEvent : MonitoringEvent
{
    public ServerStatusChangedEvent(
        ServerId serverId,
        ServerStatus previous,
        ServerStatus current,
        DateTimeOffset occurredAt)
        : base(occurredAt)
    {
        ServerId = serverId;
        Previous = previous;
        Current = current;
    }

    public ServerId ServerId { get; }

    public ServerStatus Previous { get; }

    public ServerStatus Current { get; }
}

/// <summary>Un incident a été ouvert, acquitté, rétabli ou résolu.</summary>
public sealed record IncidentChangedEvent : MonitoringEvent
{
    public IncidentChangedEvent(
        IncidentId incidentId,
        ServerId serverId,
        IncidentStatus status,
        Severity severity,
        IncidentChangeKind change,
        DateTimeOffset occurredAt)
        : base(occurredAt)
    {
        IncidentId = incidentId;
        ServerId = serverId;
        Status = status;
        Severity = severity;
        Change = change;
    }

    public IncidentId IncidentId { get; }

    public ServerId ServerId { get; }

    public IncidentStatus Status { get; }

    public Severity Severity { get; }

    public IncidentChangeKind Change { get; }
}

public enum IncidentChangeKind
{
    Opened = 0,
    Updated = 1,
    Escalated = 2,
    Acknowledged = 3,
    Recovered = 4,
    Resolved = 5,
}

/// <summary>L'état d'un conteneur a changé.</summary>
public sealed record DockerContainerChangedEvent : MonitoringEvent
{
    public DockerContainerChangedEvent(
        ServerId serverId,
        DockerContainerId containerId,
        DockerContainerStatus status,
        DateTimeOffset occurredAt)
        : base(occurredAt)
    {
        ServerId = serverId;
        ContainerId = containerId;
        Status = status;
    }

    public ServerId ServerId { get; }

    public DockerContainerId ContainerId { get; }

    public DockerContainerStatus Status { get; }
}

/// <summary>Un cycle de collecte s'est terminé, avec le nombre d'hôtes traités et en échec.</summary>
public sealed record CollectionCycleCompletedEvent : MonitoringEvent
{
    public CollectionCycleCompletedEvent(
        int succeeded,
        int failed,
        TimeSpan duration,
        DateTimeOffset occurredAt)
        : base(occurredAt)
    {
        Succeeded = succeeded;
        Failed = failed;
        Duration = duration;
    }

    public int Succeeded { get; }

    public int Failed { get; }

    public TimeSpan Duration { get; }
}
