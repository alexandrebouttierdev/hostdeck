using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Tests.Fakes;

/// <summary>
/// Dépôt de serveurs en mémoire.
///
/// Fake écrit à la main plutôt que mock généré : ces ports sont assez petits pour qu'un fake
/// tienne en quelques lignes, se lise mieux qu'une configuration de mock, et n'ajoute aucune
/// dépendance à vérifier (§41, docs/TESTING.md).
/// </summary>
internal sealed class FakeServerRepository : IServerRepository
{
    private readonly Dictionary<ServerId, Server> _servers = [];

    /// <summary>Force l'échec de la prochaine écriture, pour tester le chemin de compensation.</summary>
    public Exception? FailNextWriteWith { get; set; }

    public int AddCalls { get; private set; }

    public int UpdateCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public void Seed(params Server[] servers)
    {
        foreach (var server in servers)
        {
            _servers[server.Id] = server;
        }
    }

    public Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Server>>([.. _servers.Values]);
    }

    public Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_servers.GetValueOrDefault(id));
    }

    public Task AddAsync(Server server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCalls++;
        ThrowIfConfigured();
        _servers[server.Id] = server;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Server server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCalls++;
        ThrowIfConfigured();
        _servers[server.Id] = server;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCalls++;
        ThrowIfConfigured();
        _servers.Remove(id);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsWithEndpointAsync(
        HostAddress address,
        Port port,
        ServerId? excluding = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _servers.Values.Any(server =>
            server.Id != excluding
            && server.Port == port
            && string.Equals(server.Address.Value, address.Value, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    private void ThrowIfConfigured()
    {
        if (FailNextWriteWith is { } exception)
        {
            FailNextWriteWith = null;
            throw exception;
        }
    }
}

/// <summary>Dépôt de métriques en mémoire.</summary>
internal sealed class FakeMetricsRepository : IMetricsRepository
{
    private readonly Dictionary<Guid, LatestMetricDto> _latest = [];
    private readonly List<MetricSample> _samples = [];

    public IReadOnlyList<MetricSample> Samples => _samples;

    public void SeedLatest(params LatestMetricDto[] metrics)
    {
        foreach (var metric in metrics)
        {
            _latest[metric.ServerId] = metric;
        }
    }

    public Task AddBatchAsync(
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _samples.AddRange(samples);
        return Task.CompletedTask;
    }

    public Task<LatestMetricDto?> GetLatestAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_latest.GetValueOrDefault(serverId.Value));
    }

    public Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<LatestMetricDto>>([.. _latest.Values]);
    }

    public Task<MetricHistoryDto> GetHistoryAsync(
        MetricHistoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new MetricHistoryDto
        {
            ServerId = request.ServerId,
            From = request.From,
            To = request.To,
            BucketSize = TimeSpan.FromMinutes(1),
            Series = [],
        });
    }

    public Task<IReadOnlyList<MetricStatisticsDto>> GetStatisticsAsync(
        ServerId serverId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<MetricSeriesKind> series,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MetricStatisticsDto>>([]);
    }

    public Task<int> PruneOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = _samples.RemoveAll(sample => sample.ObservedAt < cutoff);
        return Task.FromResult(removed);
    }
}

/// <summary>Dépôt d'incidents en mémoire.</summary>
internal sealed class FakeIncidentRepository : IIncidentRepository
{
    private readonly Dictionary<IncidentId, Incident> _incidents = [];
    private readonly Dictionary<IncidentId, List<IncidentEventDto>> _timelines = [];

    public void Seed(params Incident[] incidents)
    {
        foreach (var incident in incidents)
        {
            _incidents[incident.Id] = incident;
        }
    }

    public Task<IReadOnlyList<Incident>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Incident>>(
            [.. _incidents.Values.Where(incident => incident.IsActive)]);
    }

    public Task<IReadOnlyList<IncidentDto>> QueryAsync(
        IncidentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<IncidentDto>>([]);
    }

    public Task<Incident?> FindAsync(IncidentId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_incidents.GetValueOrDefault(id));
    }

    public Task<Incident?> FindActiveAsync(
        ServerId serverId,
        AlertRuleId ruleId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var match = _incidents.Values.FirstOrDefault(incident =>
            incident.ServerId == serverId && incident.RuleId == ruleId && incident.IsActive);

        return Task.FromResult(match);
    }

    public Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _incidents[incident.Id] = incident;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _incidents[incident.Id] = incident;
        return Task.CompletedTask;
    }

    public Task AppendEventAsync(
        IncidentId incidentId,
        IncidentEventDto incidentEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_timelines.TryGetValue(incidentId, out var timeline))
        {
            timeline = [];
            _timelines[incidentId] = timeline;
        }

        timeline.Add(incidentEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IncidentEventDto>> GetTimelineAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<IncidentEventDto>>(
            _timelines.GetValueOrDefault(incidentId, []));
    }

    public Task<int> PruneResolvedOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }
}

/// <summary>Dépôt de règles d'alerte en mémoire.</summary>
internal sealed class FakeAlertRuleRepository : IAlertRuleRepository
{
    private readonly Dictionary<AlertRuleId, AlertRule> _rules = [];

    public void Seed(params AlertRule[] rules)
    {
        foreach (var rule in rules)
        {
            _rules[rule.Id] = rule;
        }
    }

    public Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AlertRule>>([.. _rules.Values]);
    }

    public Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AlertRule>>(
            [.. _rules.Values.Where(rule => rule.IsEnabled)]);
    }

    public Task<AlertRule?> FindAsync(AlertRuleId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_rules.GetValueOrDefault(id));
    }

    public Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AlertRuleId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _rules.Remove(id);
        return Task.CompletedTask;
    }
}

/// <summary>Dépôt de paramètres en mémoire.</summary>
internal sealed class FakeSettingsRepository : ISettingsRepository
{
    private SettingsSnapshot _snapshot = SettingsSnapshot.Default;

    public SettingsSnapshot Current => _snapshot;

    public Task<SettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_snapshot);
    }

    public Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _snapshot = settings;
        return Task.CompletedTask;
    }
}
