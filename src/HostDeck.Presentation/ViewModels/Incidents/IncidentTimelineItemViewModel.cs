using System;
using System.Globalization;
using HostDeck.Application.Dtos.Incidents;

namespace HostDeck.Presentation.ViewModels.Incidents;

/// <summary>Ligne de chronologie affichée dans le panneau diagnostic.</summary>
public sealed class IncidentTimelineItemViewModel
{
    public IncidentTimelineItemViewModel(IncidentEventDto eventDto)
    {
        OccurredAt = eventDto.OccurredAt;
        Label = FormatKind(eventDto.Kind);
        Detail = FormatDetail(eventDto);
    }

    public DateTimeOffset OccurredAt { get; }

    public string TimeText => OccurredAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public string Label { get; }

    public string? Detail { get; }

    private static string FormatKind(IncidentEventKind kind) => kind switch
    {
        IncidentEventKind.ConditionDetected => "Condition détectée",
        IncidentEventKind.Opened => "Incident ouvert",
        IncidentEventKind.ThresholdBreached => "Seuil franchi",
        IncidentEventKind.Updated => "Mise à jour",
        IncidentEventKind.Escalated => "Escalade",
        IncidentEventKind.Acknowledged => "Acquitté",
        IncidentEventKind.Recovered => "Rétabli",
        IncidentEventKind.Resolved => "Résolu",
        IncidentEventKind.Notified => "Notification envoyée",
        _ => kind.ToString(),
    };

    private static string? FormatDetail(IncidentEventDto eventDto)
    {
        if (!string.IsNullOrWhiteSpace(eventDto.Detail))
        {
            return eventDto.Detail;
        }

        if (!string.IsNullOrWhiteSpace(eventDto.Actor))
        {
            return $"Par {eventDto.Actor}";
        }

        return null;
    }
}
