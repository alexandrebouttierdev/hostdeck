using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Infrastructure.Monitoring;

/// <summary>
/// Évalue les règles d'alerte après une collecte et ouvre / met à jour / rétablit les
/// incidents, en respectant durée de confirmation et cooldown (§20, §21).
/// </summary>
internal sealed class IncidentEvaluationEngine
{
    private readonly IAlertRuleRepository _rules;
    private readonly IIncidentRepository _incidents;
    private readonly ISettingsRepository _settings;
    private readonly IDesktopNotificationService _notifications;
    private readonly IMonitoringEventBus _events;
    private readonly IClock _clock;

    public IncidentEvaluationEngine(
        IAlertRuleRepository rules,
        IIncidentRepository incidents,
        ISettingsRepository settings,
        IDesktopNotificationService notifications,
        IMonitoringEventBus events,
        IClock clock)
    {
        _rules = rules;
        _incidents = incidents;
        _settings = settings;
        _notifications = notifications;
        _events = events;
        _clock = clock;
    }

    public async Task EvaluateAsync(
        Server server,
        MetricSample? sample,
        ServerStatus status,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(state);

        var rules = await _rules.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
        var applicable = rules
            .Where(rule => rule.Scope.Covers(server))
            .GroupBy(rule => rule.Metric)
            .Select(group => group.OrderByDescending(rule => rule.Scope.Specificity).First())
            .ToList();

        var now = _clock.UtcNow;

        foreach (var rule in applicable)
        {
            var (breached, observed) = IsBreached(rule, sample, status);
            if (breached)
            {
                await HandleBreachAsync(server, rule, observed, now, state, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await HandleRecoveryAsync(server, rule, now, state, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task HandleBreachAsync(
        Server server,
        AlertRule rule,
        double? observed,
        DateTimeOffset now,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        var key = new AlertConditionKey(rule.Id.Value, rule.Metric);

        if (state.PendingCondition != key)
        {
            state.PendingCondition = key;
            state.ConditionSince = now;
        }

        var since = state.ConditionSince ?? now;
        if (now - since < rule.Duration)
        {
            return;
        }

        var existing = await _incidents
            .FindActiveAsync(server.Id, rule.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var incident = Incident.Open(IncidentId.New(), server.Id, rule, now, observed);
            await _incidents.AddAsync(incident, cancellationToken).ConfigureAwait(false);
            await _incidents.AppendEventAsync(
                incident.Id,
                new IncidentEventDto
                {
                    Kind = IncidentEventKind.Opened,
                    OccurredAt = now,
                    Detail = observed is { } value
                        ? $"Condition confirmée : {rule.Name} ({value})"
                        : $"Condition confirmée : {rule.Name}",
                },
                cancellationToken).ConfigureAwait(false);

            _events.Publish(new IncidentChangedEvent(
                incident.Id,
                server.Id,
                incident.Status,
                incident.Severity,
                IncidentChangeKind.Opened,
                now));

            await TryNotifyOpenedAsync(server, incident, rule, cancellationToken).ConfigureAwait(false);
            return;
        }

        existing.RecordObservation(observed, now);
        await _incidents.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        _events.Publish(new IncidentChangedEvent(
            existing.Id,
            server.Id,
            existing.Status,
            existing.Severity,
            IncidentChangeKind.Updated,
            now));
    }

    private async Task TryNotifyOpenedAsync(
        Server server,
        Incident incident,
        AlertRule rule,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.DesktopNotificationsEnabled)
        {
            return;
        }

        if (incident.Severity < settings.MinimumNotificationSeverity)
        {
            return;
        }

        await _notifications.NotifyIncidentAsync(
            new IncidentNotification
            {
                IncidentId = incident.Id.Value,
                Severity = incident.Severity,
                ServerName = server.Name.Value,
                Title = $"Incident {incident.Severity} — {server.Name.Value}",
                Body = rule.Name,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleRecoveryAsync(
        Server server,
        AlertRule rule,
        DateTimeOffset now,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        if (state.PendingCondition?.RuleId == rule.Id.Value)
        {
            state.PendingCondition = null;
            state.ConditionSince = null;
        }

        var existing = await _incidents
            .FindActiveAsync(server.Id, rule.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || existing.Status == IncidentStatus.Recovered)
        {
            return;
        }

        existing.Recover(now);
        await _incidents.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        await _incidents.AppendEventAsync(
            existing.Id,
            new IncidentEventDto
            {
                Kind = IncidentEventKind.Recovered,
                OccurredAt = now,
                Detail = "Condition rétablie",
            },
            cancellationToken).ConfigureAwait(false);

        _events.Publish(new IncidentChangedEvent(
            existing.Id,
            server.Id,
            existing.Status,
            existing.Severity,
            IncidentChangeKind.Recovered,
            now));
    }

    private static (bool Breached, double? Observed) IsBreached(
        AlertRule rule,
        MetricSample? sample,
        ServerStatus status)
    {
        if (rule.Metric.IsStateBased())
        {
            return rule.Metric switch
            {
                MonitoredMetric.ServerUnavailable =>
                    (status is ServerStatus.Offline or ServerStatus.AuthenticationFailed, null),
                MonitoredMetric.GatewayUnavailable =>
                    (status == ServerStatus.GatewayUnavailable, null),
                _ => (false, null),
            };
        }

        if (sample is null)
        {
            return (false, null);
        }

        double? value = rule.Metric switch
        {
            MonitoredMetric.CpuUsage => sample.Cpu.Total.Value,
            MonitoredMetric.MemoryUsage => sample.Memory.UsedRatio.Value,
            MonitoredMetric.DiskUsage => sample.BusiestDisk?.UsedRatio.Value ?? 0d,
            MonitoredMetric.LoadAverage => sample.Load.OneMinute,
            MonitoredMetric.SwapUsage => sample.Swap.UsedRatio.Value,
            MonitoredMetric.SshLatency => null,
            _ => null,
        };

        if (value is null)
        {
            return (false, null);
        }

        return (rule.IsConditionMet(value.Value), value);
    }
}
