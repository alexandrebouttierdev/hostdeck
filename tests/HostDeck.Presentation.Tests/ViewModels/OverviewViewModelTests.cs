using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Incidents;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Overview;
using Xunit;

namespace HostDeck.Presentation.Tests.ViewModels;

public sealed class OverviewViewModelTests
{
    [Fact]
    public async Task RefreshWithLatestComputesFleetAggregatesAndCharts()
    {
        var server = CreateServer("vps-ovh", "10.0.0.8", ServerStatus.Online);
        var latest = new LatestMetricDto
        {
            ServerId = server.Id.Value,
            ObservedAt = DateTimeOffset.UtcNow,
            CpuPercent = 42,
            MemoryPercent = 61,
            DiskPercent = 33,
            LoadOneMinute = 0.4,
            NetworkReceivedBytesPerSecond = 2048,
            NetworkTransmittedBytesPerSecond = 1024,
        };

        var metrics = new StubMetricsRepository(
            latestByServer: new Dictionary<Guid, LatestMetricDto> { [server.Id.Value] = latest },
            historyFactory: request => BuildHistory(request, cpu: 40, memory: 60));

        using var vm = CreateViewModel([server], metrics);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(vm.IsEmpty);
        Assert.Equal(1, vm.HostCount);
        Assert.Equal(1, vm.OnlineHostCount);
        Assert.Equal(0, vm.WarningHostCount);
        Assert.Equal(0, vm.ProblemHostCount);
        Assert.Equal(42d, vm.CpuGaugeValue);
        Assert.Equal(61d, vm.MemoryGaugeValue);
        Assert.Equal(33d, vm.DiskGaugeValue);
        Assert.NotNull(vm.NetworkGaugeValue);
        Assert.Equal(100d, vm.AvailabilityPercent);
        Assert.Equal("1 opérationnel · 0 avertissement · 0 problème", vm.HostsStatusSubtitle);
        Assert.Contains("sous surveillance", vm.CollectionDetailText, StringComparison.Ordinal);
        Assert.DoesNotContain("En attente du premier hôte", vm.CollectionDetailText, StringComparison.Ordinal);
        Assert.Contains("42%", vm.CpuAverageText, StringComparison.Ordinal);
        Assert.Contains("42%", vm.CpuPeakText, StringComparison.Ordinal);
        Assert.Contains("KiB/s", vm.NetworkInText, StringComparison.Ordinal);
        Assert.NotNull(vm.CpuSeries);
        Assert.True(vm.CpuSeries!.Count >= 1);
        Assert.Contains(vm.CpuSeries, s => s.Name is "user" or "CPU" or "CPU flotte");
        Assert.NotNull(vm.MemorySeries);
        Assert.Single(vm.TopHosts);
        Assert.Equal("vps-ovh", vm.TopHosts[0].Name);
    }

    [Fact]
    public async Task RefreshWithoutHostsKeepsEmptyAggregates()
    {
        using var vm = CreateViewModel([], new StubMetricsRepository());
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.IsEmpty);
        Assert.Null(vm.CpuGaugeValue);
        Assert.Null(vm.AvailabilityPercent);
        Assert.Equal("Indisponible sans flotte", vm.AvailabilitySubtitle);
        Assert.Equal("En attente du premier hôte.", vm.CollectionDetailText);
        Assert.Null(vm.CpuSeries);
        Assert.Null(vm.MemorySeries);
    }

    [Fact]
    public async Task AverageAggregatesAcrossHostsWithLatest()
    {
        var online = CreateServer("a", "10.0.0.1", ServerStatus.Online);
        var warning = CreateServer("b", "10.0.0.2", ServerStatus.Unknown);
        var offline = CreateServer("c", "10.0.0.3", ServerStatus.Offline);

        var latest = new Dictionary<Guid, LatestMetricDto>
        {
            [online.Id.Value] = Metric(online.Id.Value, cpu: 10, mem: 20, disk: 30, rx: 100, tx: 50),
            [warning.Id.Value] = Metric(warning.Id.Value, cpu: 30, mem: 40, disk: 50, rx: 200, tx: 150),
        };

        var metrics = new StubMetricsRepository(latest);
        using var vm = CreateViewModel([online, warning, offline], metrics);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, vm.HostCount);
        Assert.Equal(1, vm.OnlineHostCount);
        Assert.Equal(1, vm.WarningHostCount);
        Assert.Equal(1, vm.ProblemHostCount);
        Assert.Equal(20d, vm.CpuGaugeValue);
        Assert.Equal(30d, vm.MemoryGaugeValue);
        Assert.Equal(40d, vm.DiskGaugeValue);
        Assert.Contains("30%", vm.CpuPeakText, StringComparison.Ordinal);
        Assert.Equal(100d / 3d, vm.AvailabilityPercent!.Value, precision: 5);
    }

    [Fact]
    public async Task MetricUpdatedEventRefreshesAggregatesFromLatest()
    {
        var server = CreateServer("vps-ovh", "10.0.0.8", ServerStatus.Unknown);
        var bus = new ImmediateEventBus();
        var metrics = new StubMetricsRepository();
        using var vm = CreateViewModel([server], metrics, bus);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Null(vm.CpuGaugeValue);
        Assert.Equal(0, vm.OnlineHostCount);

        bus.Publish(new MetricUpdatedEvent(
            server.Id,
            Metric(server.Id.Value, cpu: 55, mem: 70, disk: 12, rx: 4096, tx: 512),
            DateTimeOffset.UtcNow));

        Assert.Equal(55d, vm.CpuGaugeValue);
        Assert.Equal(70d, vm.MemoryGaugeValue);
        Assert.Equal(1, vm.OnlineHostCount);
        Assert.Equal(100d, vm.AvailabilityPercent);
    }

    [Fact]
    public void NetworkGaugeUsesDocumentedMbpsScale()
    {
        // 125_000_000 B/s = 1000 Mbit/s → 100 % sur la référence 1 Gbit/s.
        var bytesPerSecond = 125_000_000d;
        var totalMbps = bytesPerSecond * 8d / 1_000_000d;
        var gauge = Math.Min(100d, totalMbps / OverviewViewModel.NetworkGaugeReferenceMbps * 100d);
        Assert.Equal(1000d, OverviewViewModel.NetworkGaugeReferenceMbps);
        Assert.Equal(100d, gauge);
    }

    private static LatestMetricDto Metric(
        Guid serverId,
        double cpu,
        double mem,
        double disk,
        double rx,
        double tx)
        => new()
        {
            ServerId = serverId,
            ObservedAt = DateTimeOffset.UtcNow,
            CpuPercent = cpu,
            MemoryPercent = mem,
            DiskPercent = disk,
            NetworkReceivedBytesPerSecond = rx,
            NetworkTransmittedBytesPerSecond = tx,
        };

    private static OverviewViewModel CreateViewModel(
        IReadOnlyList<Server> servers,
        StubMetricsRepository metrics,
        IMonitoringEventBus? bus = null)
    {
        return new OverviewViewModel(
            new GetServersUseCase(new StubServerRepository(servers), metrics),
            new GetIncidentsUseCase(new StubIncidentRepository()),
            new GetMetricHistoryUseCase(metrics),
            bus ?? new ImmediateEventBus(),
            new StubDialogService(),
            new ImmediateDispatcher());
    }

    private static Server CreateServer(string name, string address, ServerStatus status)
    {
        var id = ServerId.New();
        var server = new Server(
            id,
            new ServerName(name),
            HostAddress.Parse(address),
            new Port(22),
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default);
        if (status != ServerStatus.Unknown)
        {
            server.RecordCollection(status, DateTimeOffset.UtcNow);
        }

        return server;
    }

    private static MetricHistoryDto BuildHistory(MetricHistoryRequestDto request, double cpu, double memory)
    {
        var t0 = request.From;
        var t1 = request.From + ((request.To - request.From) / 2);
        var t2 = request.To;
        MetricPointDto[] Points(double value) =>
        [
            new MetricPointDto(t0, value - 1, value, value + 1),
            new MetricPointDto(t1, value - 1, value, value + 1),
            new MetricPointDto(t2, value - 1, value, value + 1),
        ];

        var series = new List<MetricSeriesDto>();
        foreach (var kind in request.Series)
        {
            if (kind == MetricSeriesKind.MemoryUsed)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(memory) });
            }
            else if (kind == MetricSeriesKind.NetworkReceived)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(2) });
            }
            else if (kind == MetricSeriesKind.CpuUser)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(cpu * 0.6) });
            }
            else if (kind == MetricSeriesKind.CpuSystem)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(cpu * 0.3) });
            }
            else if (kind == MetricSeriesKind.CpuIoWait)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(cpu * 0.1) });
            }
            else if (kind is MetricSeriesKind.CpuNice or MetricSeriesKind.CpuSteal)
            {
                // Modes absents volontairement : le stub ne fabrique pas de séries vides inutiles.
            }
            else if (kind == MetricSeriesKind.CpuTotal)
            {
                series.Add(new MetricSeriesDto { Kind = kind, Points = Points(cpu) });
            }
        }

        return new MetricHistoryDto
        {
            ServerId = request.ServerId,
            From = request.From,
            To = request.To,
            BucketSize = TimeSpan.FromMinutes(1),
            Series = series,
        };
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<bool> ShowAddHostAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class ImmediateEventBus : IMonitoringEventBus
    {
        private readonly List<(Type Type, Delegate Handler)> _handlers = [];

        public void Publish(MonitoringEvent monitoringEvent)
        {
            foreach (var (type, handler) in _handlers)
            {
                if (type.IsInstanceOfType(monitoringEvent))
                {
                    handler.DynamicInvoke(monitoringEvent);
                }
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : MonitoringEvent
        {
            _handlers.Add((typeof(TEvent), handler));
            return new Noop();
        }

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class StubServerRepository(IReadOnlyList<Server> servers) : IServerRepository
    {
        public Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(servers);

        public Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default)
            => Task.FromResult(servers.FirstOrDefault(s => s.Id == id));

        public Task AddAsync(Server server, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(Server server, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ExistsWithEndpointAsync(
            HostAddress address,
            Port port,
            ServerId? excluding = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubMetricsRepository : IMetricsRepository
    {
        private readonly IReadOnlyDictionary<Guid, LatestMetricDto> _latest;
        private readonly Func<MetricHistoryRequestDto, MetricHistoryDto>? _historyFactory;

        public StubMetricsRepository(
            IReadOnlyDictionary<Guid, LatestMetricDto>? latestByServer = null,
            Func<MetricHistoryRequestDto, MetricHistoryDto>? historyFactory = null)
        {
            _latest = latestByServer ?? new Dictionary<Guid, LatestMetricDto>();
            _historyFactory = historyFactory;
        }

        public Task AddBatchAsync(IReadOnlyList<MetricSample> samples, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<LatestMetricDto?> GetLatestAsync(ServerId serverId, CancellationToken cancellationToken = default)
            => Task.FromResult(_latest.TryGetValue(serverId.Value, out var metric) ? metric : null);

        public Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LatestMetricDto>>([.. _latest.Values]);

        public Task<MetricHistoryDto> GetHistoryAsync(
            MetricHistoryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (_historyFactory is not null)
            {
                return Task.FromResult(_historyFactory(request));
            }

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
            => Task.FromResult<IReadOnlyList<MetricStatisticsDto>>([]);

        public Task<int> PruneOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class StubIncidentRepository : IIncidentRepository
    {
        public Task<IReadOnlyList<Incident>> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Incident>>([]);

        public Task<IReadOnlyList<IncidentDto>> QueryAsync(
            IncidentFilterDto filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IncidentDto>>([]);

        public Task<Incident?> FindAsync(IncidentId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Incident?>(null);

        public Task<Incident?> FindActiveAsync(
            ServerId serverId,
            AlertRuleId ruleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Incident?>(null);

        public Task AddAsync(Incident incident, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AppendEventAsync(
            IncidentId incidentId,
            IncidentEventDto incidentEvent,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<IncidentEventDto>> GetTimelineAsync(
            IncidentId incidentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IncidentEventDto>>([]);

        public Task<int> PruneResolvedOlderThanAsync(
            DateTimeOffset cutoff,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
