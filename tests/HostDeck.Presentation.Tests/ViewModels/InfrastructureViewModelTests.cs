using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.ViewModels.Infrastructure;
using Xunit;

namespace HostDeck.Presentation.Tests.ViewModels;

public sealed class InfrastructureViewModelTests
{
    [Fact]
    public void HasSelectionIsFalseUntilARowIsChosen()
    {
        var vm = CreateViewModel([]);
        Assert.False(vm.HasSelection);
        Assert.Null(vm.SelectedHost);
    }

    [Fact]
    public async Task RefreshMapsServersWithoutInventingSparklineSeries()
    {
        var server = CreateServer("web-01", "10.0.0.1");
        var vm = CreateViewModel([server]);
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
        var vm = CreateViewModel([server]);
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

    private static InfrastructureViewModel CreateViewModel(IReadOnlyList<Server> servers)
    {
        var repo = new StubServerRepository(servers);
        var metrics = new StubMetricsRepository();
        return new InfrastructureViewModel(new GetServersUseCase(repo, metrics));
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
