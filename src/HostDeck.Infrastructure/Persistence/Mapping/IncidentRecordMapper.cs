using System;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Persistence.Records;

namespace HostDeck.Infrastructure.Persistence.Mapping;

/// <summary>Conversions entre <see cref="Incident"/> et <see cref="IncidentRecord"/>.</summary>
internal static class IncidentRecordMapper
{
    public static IncidentRecord ToRecord(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var record = new IncidentRecord
        {
            Id = incident.Id.Value,
            ServerId = incident.ServerId.Value,
            RuleId = incident.RuleId.Value,
            Metric = (int)incident.Metric,
            StartedAt = incident.StartedAt,
            ThresholdValue = incident.ThresholdValue,
        };

        ApplyTo(record, incident);
        return record;
    }

    public static void ApplyTo(IncidentRecord record, Incident incident)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(incident);

        record.Severity = (int)incident.Severity;
        record.Status = (int)incident.Status;
        record.LastUpdatedAt = incident.LastUpdatedAt;
        record.AcknowledgedAt = incident.AcknowledgedAt;
        record.AcknowledgedBy = incident.AcknowledgedBy;
        record.RecoveredAt = incident.RecoveredAt;
        record.ResolvedAt = incident.ResolvedAt;
        record.CurrentValue = incident.CurrentValue;
        record.LastNotifiedAt = incident.LastNotifiedAt;
    }

    /// <summary>
    /// Réhydrate l'agrégat par sa fabrique de restauration, et non en rejouant ses
    /// transitions : celles-ci écraseraient les horodatages d'origine et refuseraient de
    /// relire un incident déjà résolu.
    /// </summary>
    public static Incident ToDomain(IncidentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Incident.Restore(
            new IncidentId(record.Id),
            new ServerId(record.ServerId),
            new AlertRuleId(record.RuleId),
            (MonitoredMetric)record.Metric,
            (Severity)record.Severity,
            (IncidentStatus)record.Status,
            record.StartedAt,
            record.LastUpdatedAt,
            record.AcknowledgedAt,
            record.AcknowledgedBy,
            record.RecoveredAt,
            record.ResolvedAt,
            record.CurrentValue,
            record.ThresholdValue,
            record.LastNotifiedAt);
    }

    public static IncidentEventRecord ToRecord(IncidentId incidentId, IncidentEventDto incidentEvent)
    {
        ArgumentNullException.ThrowIfNull(incidentEvent);

        return new IncidentEventRecord
        {
            IncidentId = incidentId.Value,
            OccurredAt = incidentEvent.OccurredAt,
            Kind = (int)incidentEvent.Kind,
            Detail = incidentEvent.Detail,
            Actor = incidentEvent.Actor,
        };
    }

    public static IncidentEventDto ToDto(IncidentEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new IncidentEventDto
        {
            OccurredAt = record.OccurredAt,
            Kind = (IncidentEventKind)record.Kind,
            Detail = record.Detail,
            Actor = record.Actor,
        };
    }
}
