using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Persistence.Records;

namespace HostDeck.Infrastructure.Persistence.Mapping;

/// <summary>Conversions entre <see cref="AlertRule"/> et <see cref="AlertRuleRecord"/>.</summary>
internal static class AlertRuleRecordMapper
{
    public static AlertRuleRecord ToRecord(AlertRule rule, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var record = new AlertRuleRecord
        {
            Id = rule.Id.Value,
            Metric = (int)rule.Metric,
            CreatedAt = now,
        };

        ApplyTo(record, rule, now);
        return record;
    }

    public static void ApplyTo(AlertRuleRecord record, AlertRule rule, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(rule);

        record.Name = rule.Name;
        record.Description = rule.Description;
        record.Severity = (int)rule.Severity;
        record.ComparisonOperator = rule.Threshold is null ? null : (int)rule.Threshold.Comparison;
        record.ThresholdValue = rule.Threshold?.Value;
        record.DurationSeconds = (int)rule.Duration.TotalSeconds;
        record.CooldownSeconds = (int)rule.Cooldown.TotalSeconds;
        record.ScopeKind = (int)rule.Scope.Kind;
        record.ScopeGroup = rule.Scope.Group?.Name;
        record.ScopeServerId = rule.Scope.ServerId?.Value;
        record.IsEnabled = rule.IsEnabled;
        record.UpdatedAt = now;
    }

    public static AlertRule ToDomain(AlertRuleRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var metric = (MonitoredMetric)record.Metric;

        return new AlertRule(
            new AlertRuleId(record.Id),
            record.Name,
            metric,
            (Severity)record.Severity,
            ToScope(record),
            ToThreshold(record, metric),
            TimeSpan.FromSeconds(record.DurationSeconds),
            TimeSpan.FromSeconds(record.CooldownSeconds),
            record.Description,
            record.IsEnabled);
    }

    private static AlertScope ToScope(AlertRuleRecord record) => (AlertScopeKind)record.ScopeKind switch
    {
        AlertScopeKind.Group when record.ScopeGroup is { } group => AlertScope.ForGroup(new ServerGroup(group)),
        AlertScopeKind.Server when record.ScopeServerId is { } serverId => AlertScope.ForServer(new ServerId(serverId)),
        _ => AlertScope.Global,
    };

    /// <summary>
    /// Reconstruit le seuil. Une métrique d'état binaire n'en admet aucun : le constructeur
    /// du domaine rejetterait la règle si on lui en fournissait un.
    /// </summary>
    private static Threshold? ToThreshold(AlertRuleRecord record, MonitoredMetric metric)
    {
        if (metric.IsStateBased())
        {
            return null;
        }

        if (record.ComparisonOperator is not { } comparison || record.ThresholdValue is not { } value)
        {
            return null;
        }

        return new Threshold((ComparisonOperator)comparison, value, metric);
    }
}
