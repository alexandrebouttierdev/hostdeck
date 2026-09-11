using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Errors;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Infrastructure.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}

internal sealed class RecordingEventBus : IMonitoringEventBus
{
    private readonly List<MonitoringEvent> _published = [];

    public IReadOnlyList<MonitoringEvent> Published => _published;

    public void Publish(MonitoringEvent monitoringEvent) => _published.Add(monitoringEvent);

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : MonitoringEvent => NullSubscription.Instance;

    private sealed class NullSubscription : IDisposable
    {
        public static readonly NullSubscription Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class FakeSshConnection : ISshConnection
{
    private readonly Func<string, CommandResult> _execute;

    public FakeSshConnection(string host, TimeSpan latency, Func<string, CommandResult> execute)
    {
        Host = host;
        Latency = latency;
        _execute = execute;
    }

    public string Host { get; }

    public TimeSpan Latency { get; }

    public int ExecuteCalls { get; private set; }

    public Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteCalls++;
        return Task.FromResult(_execute(command));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeSshConnectionFactory : ISshConnectionFactory
{
    private readonly Func<Server, CancellationToken, Task<ISshConnection>> _connect;

    public FakeSshConnectionFactory(Func<Server, CancellationToken, Task<ISshConnection>> connect)
    {
        _connect = connect;
    }

    public int ConnectCalls { get; private set; }

    public Task<ISshConnection> ConnectAsync(Server server, CancellationToken cancellationToken = default)
    {
        ConnectCalls++;
        return _connect(server, cancellationToken);
    }
}

internal sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public bool Available { get; set; } = true;

    public Task<SecretMaterial> ReadAsync(CredentialReference reference, CancellationToken cancellationToken = default)
    {
        if (!_secrets.TryGetValue(reference.Key, out var secret))
        {
            throw new CredentialException($"missing {reference.Key}", CredentialFailure.NotFound);
        }

        return Task.FromResult(new SecretMaterial(secret));
    }

    public Task WriteAsync(CredentialReference reference, ReadOnlyMemory<char> secret, CancellationToken cancellationToken = default)
    {
        _secrets[reference.Key] = secret.ToString();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CredentialReference reference, CancellationToken cancellationToken = default)
    {
        _secrets.Remove(reference.Key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(CredentialReference reference, CancellationToken cancellationToken = default) =>
        Task.FromResult(_secrets.ContainsKey(reference.Key));

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Available);
}

internal sealed class FakeServerRepository : IServerRepository
{
    private readonly Dictionary<Guid, Server> _servers = [];

    public List<Server> All => [.. _servers.Values];

    public Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Server>>([.. _servers.Values]);

    public Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_servers.TryGetValue(id.Value, out var server) ? server : null);

    public Task AddAsync(Server server, CancellationToken cancellationToken = default)
    {
        _servers[server.Id.Value] = server;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Server server, CancellationToken cancellationToken = default)
    {
        _servers[server.Id.Value] = server;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default)
    {
        _servers.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsWithEndpointAsync(
        HostAddress address,
        Port port,
        ServerId? excluding = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

internal sealed class FakeMetricsRepository : IMetricsRepository
{
    public List<MetricSample> Samples { get; } = [];

    public Task AddBatchAsync(IReadOnlyList<MetricSample> samples, CancellationToken cancellationToken = default)
    {
        Samples.AddRange(samples);
        return Task.CompletedTask;
    }

    public Task<LatestMetricDto?> GetLatestAsync(ServerId serverId, CancellationToken cancellationToken = default) =>
        Task.FromResult<LatestMetricDto?>(null);

    public Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LatestMetricDto>>([]);

    public Task<MetricHistoryDto> GetHistoryAsync(MetricHistoryRequestDto request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<MetricStatisticsDto>> GetStatisticsAsync(
        ServerId serverId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<MetricSeriesKind> series,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MetricStatisticsDto>>([]);

    public Task<int> PruneOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

internal sealed class FakeSettingsRepository : ISettingsRepository
{
    public SettingsSnapshot Snapshot { get; set; } = SettingsSnapshot.Default with
    {
        CollectionRetryCount = 2,
        CollectionTimeoutSeconds = 5,
        MaxConcurrentCollections = 4,
        DefaultCollectionIntervalSeconds = 60,
    };

    public Task<SettingsSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshot);

    public Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        Snapshot = settings;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAlertRuleRepository : IAlertRuleRepository
{
    public List<AlertRule> Rules { get; } = [];

    public Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AlertRule>>(Rules);

    public Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AlertRule>>(Rules.FindAll(rule => rule.IsEnabled));

    public Task<AlertRule?> FindAsync(AlertRuleId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rules.Find(rule => rule.Id.Equals(id)));

    public Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        Rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(AlertRuleId id, CancellationToken cancellationToken = default)
    {
        Rules.RemoveAll(rule => rule.Id.Equals(id));
        return Task.CompletedTask;
    }
}

internal sealed class FakeIncidentRepository : IIncidentRepository
{
    public List<Incident> Incidents { get; } = [];

    public Task<IReadOnlyList<Incident>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Incident>>(Incidents.FindAll(incident => incident.IsActive));

    public Task<IReadOnlyList<HostDeck.Application.Dtos.Incidents.IncidentDto>> QueryAsync(
        HostDeck.Application.Dtos.Incidents.IncidentFilterDto filter,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HostDeck.Application.Dtos.Incidents.IncidentDto>>([]);

    public Task<Incident?> FindAsync(IncidentId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Incidents.Find(incident => incident.Id.Equals(id)));

    public Task<Incident?> FindActiveAsync(ServerId serverId, AlertRuleId ruleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Incidents.Find(incident =>
            incident.ServerId.Equals(serverId)
            && incident.RuleId.Equals(ruleId)
            && incident.IsActive));

    public Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        Incidents.Add(incident);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AppendEventAsync(
        IncidentId incidentId,
        HostDeck.Application.Dtos.Incidents.IncidentEventDto incidentEvent,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<HostDeck.Application.Dtos.Incidents.IncidentEventDto>> GetTimelineAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HostDeck.Application.Dtos.Incidents.IncidentEventDto>>([]);

    public Task<int> PruneResolvedOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}

internal sealed class NoOpDesktopNotificationService : IDesktopNotificationService
{
    public Task NotifyIncidentAsync(IncidentNotification notification, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
