using HostDeck.Application.Docker;
using HostDeck.Application.Ports;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.ViewModels.Docker;
using Xunit;

namespace HostDeck.Presentation.Tests.ViewModels;

public sealed class DockerViewModelTests
{
    [Fact]
    public async Task RefreshWithoutDockerHostsLeavesEmptyState()
    {
        var runtime = new StubContainerRuntime();
        var vm = new DockerViewModel(
            new ListDockerContainersUseCase(new EmptyServerRepository(), runtime),
            new StartDockerContainerUseCase(runtime),
            new StopDockerContainerUseCase(runtime),
            new RestartDockerContainerUseCase(runtime));

        await vm.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Containers);
        Assert.Null(vm.ErrorMessage);
    }

    private sealed class EmptyServerRepository : IServerRepository
    {
        public Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Server>>([]);

        public Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Server?>(null);

        public Task AddAsync(Server server, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Server server, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsWithEndpointAsync(
            HostAddress address,
            Port port,
            ServerId? excluding = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class StubContainerRuntime : IContainerRuntime
    {
        public Task<DockerHost> GetInfoAsync(ServerId serverId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DockerContainer>> ListContainersAsync(
            ServerId serverId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DockerContainer>>([]);

        public Task<Application.Dtos.Docker.DockerContainerDetailsDto> GetContainerDetailsAsync(
            ServerId serverId,
            DockerContainerId containerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DockerContainerStats> GetStatsAsync(
            ServerId serverId,
            DockerContainerId containerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StartContainerAsync(
            ServerId serverId,
            DockerContainerId containerId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopContainerAsync(
            ServerId serverId,
            DockerContainerId containerId,
            TimeSpan gracePeriod,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RestartContainerAsync(
            ServerId serverId,
            DockerContainerId containerId,
            TimeSpan gracePeriod,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public IAsyncEnumerable<Application.Dtos.Docker.ContainerLogEntryDto> StreamLogsAsync(
            ServerId serverId,
            DockerContainerId containerId,
            ContainerLogOptions options,
            CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<Application.Dtos.Docker.ContainerLogEntryDto>();
    }
}
