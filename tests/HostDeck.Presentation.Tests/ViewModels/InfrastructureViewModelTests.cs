using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Presentation.Tests.ViewModels;

public sealed class InfrastructureViewModelTests
{
    [Fact]
    public void HasSelectionIsFalseUntilARowIsChosen()
    {
        using var vm = CreateViewModel([]);
        Assert.False(vm.HasSelection);
        Assert.Null(vm.SelectedHost);
    }

    [Fact]
    public async Task RefreshMapsServersWithoutInventingSparklineSeries()
    {
        var server = CreateServer("web-01", "10.0.0.1");
        using var vm = CreateViewModel([server]);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(vm.IsEmpty);
        Assert.Single(vm.Hosts);
        Assert.Equal("web-01", vm.Hosts[0].Name);
        Assert.Null(vm.Hosts[0].CpuSeries);
        Assert.False(vm.Hosts[0].HasCpuSeries);
        Assert.Equal("—", vm.Hosts[0].CpuText);
    }

    [Fact]
    public async Task OpenHostDetailsCommandUsesHandlerWithSelectedServer()
    {
        var server = CreateServer("web-01", "10.0.0.1");
        using var vm = CreateViewModel([server]);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);
        vm.SelectedHost = vm.Hosts[0];

        ServerSummaryDto? opened = null;
        vm.OpenHostDetailsHandler = (dto, _) =>
        {
            opened = dto;
            return Task.CompletedTask;
        };
        vm.SelectedHost = vm.Hosts[0];

        Assert.True(vm.OpenHostDetailsCommand.CanExecute(null));
        await vm.OpenHostDetailsCommand.ExecuteAsync(null);
        Assert.Equal(server.Id.Value, opened!.ServerId);
    }

    [Fact]
    public async Task MetricUpdatedEventAppendsSparklineWithoutFabrication()
    {
        var server = CreateServer("web-01", "10.0.0.1");
        var bus = new ImmediateEventBus();
        using var vm = CreateViewModel([server], bus);
        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Null(vm.Hosts[0].CpuSeries);

        bus.Publish(new MetricUpdatedEvent(
            server.Id,
            new LatestMetricDto
            {
                ServerId = server.Id.Value,
                ObservedAt = DateTimeOffset.UtcNow,
                CpuPercent = 12,
                MemoryPercent = 40,
                DiskPercent = 55,
                LoadOneMinute = 0.5,
            },
            DateTimeOffset.UtcNow));

        Assert.NotNull(vm.Hosts[0].CpuSeries);
        Assert.Single(vm.Hosts[0].CpuSeries!);
        Assert.False(vm.Hosts[0].HasCpuSeries);

        bus.Publish(new MetricUpdatedEvent(
            server.Id,
            new LatestMetricDto
            {
                ServerId = server.Id.Value,
                ObservedAt = DateTimeOffset.UtcNow,
                CpuPercent = 20,
                MemoryPercent = 41,
                DiskPercent = 56,
                LoadOneMinute = 0.6,
            },
            DateTimeOffset.UtcNow));

        Assert.True(vm.Hosts[0].HasCpuSeries);
        Assert.Equal(2, vm.Hosts[0].CpuSeries!.Count);
    }

    private static InfrastructureViewModel CreateViewModel(
        IReadOnlyList<Server> servers,
        IMonitoringEventBus? bus = null)
    {
        var repo = new StubServerRepository(servers);
        var metrics = new StubMetricsRepository();
        return new InfrastructureViewModel(
            new GetServersUseCase(repo, metrics),
            new DeleteServerUseCase(repo, new StubCredentialStore(), NullLogger<DeleteServerUseCase>.Instance),
            new GetMetricHistoryUseCase(metrics),
            bus ?? new ImmediateEventBus(),
            new StubDialogService(),
            new ImmediateDispatcher());
    }

    private static Server CreateServer(string name, string address)
    {
        var id = ServerId.New();
        return new Server(
            id,
            new ServerName(name),
            HostAddress.Parse(address),
            new Port(22),
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default);
    }

    private sealed class StubDialogService : IDialogService
    {
        public Task<bool> ShowAddHostAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubCredentialStore : ICredentialStore
    {
        public Task DeleteAsync(CredentialReference reference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(CredentialReference reference, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<SecretMaterial> ReadAsync(
            CredentialReference reference,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task WriteAsync(
            CredentialReference reference,
            ReadOnlyMemory<char> secret,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
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
        public Task AddBatchAsync(IReadOnlyList<MetricSample> samples, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<LatestMetricDto?> GetLatestAsync(ServerId serverId, CancellationToken cancellationToken = default)
            => Task.FromResult<LatestMetricDto?>(null);

        public Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LatestMetricDto>>([]);

        public Task<MetricHistoryDto> GetHistoryAsync(
            MetricHistoryRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MetricHistoryDto
            {
                ServerId = request.ServerId,
                From = request.From,
                To = request.To,
                BucketSize = TimeSpan.FromMinutes(1),
                Series = [],
            });

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
}
