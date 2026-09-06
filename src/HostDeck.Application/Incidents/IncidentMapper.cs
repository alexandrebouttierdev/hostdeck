using System;
using System.Globalization;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;

namespace HostDeck.Application.Incidents;

/// <summary>
/// Conversions entre l'agrégat <see cref="Incident"/> et ses DTO.
/// </summary>
public static class IncidentMapper
{
    public static IncidentDto ToDto(
        Incident incident,
        string? serverName,
        AlertRule? rule,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return new IncidentDto
        {
            IncidentId = incident.Id.Value,
            ServerId = incident.ServerId.Value,

            // Le nom peut manquer si le serveur a été supprimé alors que son incident
            // subsiste : l'historique reste lisible plutôt que de disparaître.
            ServerName = serverName ?? "Serveur supprimé",
            Severity = incident.Severity,
            Status = incident.Status,
            Metric = incident.Metric,
            Problem = DescribeProblem(incident, rule),
            StartedAt = incident.StartedAt,
            LastUpdatedAt = incident.LastUpdatedAt,
            Duration = incident.DurationAt(now),
            AcknowledgedBy = incident.AcknowledgedBy,
            AcknowledgedAt = incident.AcknowledgedAt,
            CurrentValue = incident.CurrentValue,
            ThresholdValue = incident.ThresholdValue,
        };
    }

    /// <summary>
    /// Construit le libellé affiché dans la colonne « Problème ».
    ///
    /// Reconstruit depuis la métrique et le seuil plutôt que stocké : un libellé figé en base
    /// se désynchroniserait du seuil dès qu'une règle serait modifiée.
    /// </summary>
    public static string DescribeProblem(Incident incident, AlertRule? rule)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var metricLabel = DescribeMetric(incident.Metric);

        if (incident.Metric.IsStateBased())
        {
            return metricLabel;
        }

        var comparison = rule?.Threshold?.Comparison switch
        {
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => "≥",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "≤",
            _ => ">",
        };

        if (incident.ThresholdValue is not { } threshold)
        {
            return metricLabel;
        }

        var unit = incident.Metric.IsPercentage() ? " %" : string.Empty;

        return string.Create(
            CultureInfo.CurrentCulture,
            $"{metricLabel} {comparison} {threshold:0.##}{unit}");
    }

    /// <summary>Libellé français d'une métrique surveillée.</summary>
    public static string DescribeMetric(MonitoredMetric metric) => metric switch
    {
        MonitoredMetric.ServerUnavailable => "Serveur injoignable",
        MonitoredMetric.GatewayUnavailable => "Bastion injoignable",
        MonitoredMetric.CpuUsage => "Utilisation CPU",
        MonitoredMetric.MemoryUsage => "Utilisation mémoire",
        MonitoredMetric.DiskUsage => "Espace disque utilisé",
        MonitoredMetric.LoadAverage => "Charge système",
        MonitoredMetric.SshLatency => "Latence SSH",
        MonitoredMetric.SwapUsage => "Utilisation du swap",
        MonitoredMetric.DockerContainerStopped => "Conteneur arrêté",
        MonitoredMetric.DockerContainerUnhealthy => "Conteneur non sain",
        MonitoredMetric.DockerRestartCount => "Redémarrages de conteneur",
        _ => "Métrique inconnue",
    };
}
