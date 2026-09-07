using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Monitoring;
using HostDeck.Infrastructure.Ssh;
using HostDeck.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Monitoring;

public sealed class FleetMonitoringCoordinatorTests
{
    private static readonly string CpuStat = """
        cpu  1000 200 300 4000 50 10 20 30 0 0
        cpu0 500 100 150 2000 25 5 10 15 0 0
        cpu1 500 100 150 2000 25 5 10 15 0 0
        """;

    private static readonly string MemInfo = """
        MemTotal:        8000000 kB
        MemFree:         2000000 kB
        MemAvailable:    4000000 kB
        Buffers:          200000 kB
        Cached:          1500000 kB
        SwapTotal:       1000000 kB
        SwapFree:         800000 kB
        """;

    [Fact]
    public async Task RunCycle_SuccessfulCollect_PersistsMetricsAndPublishesEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var server = TestData.Server();
        var servers = new FakeServerRepository();
        await servers.AddAsync(server, ct);

        var metrics = new FakeMetricsRepository();
        var events = new RecordingEventBus();
        var settings = new FakeSettingsRepository();
        var alerts = new FakeAlertRuleRepository();
        var incidents = new FakeIncidentRepository();

        var factory = new FakeSshConnectionFactory((_, _) =>
            Task.FromResult<ISshConnection>(CreateConnection(server.Address.Value)));

        using var coordinator = CreateCoordinator(
            servers, settings, factory, metrics, alerts, incidents, events, clock);

        await coordinator.RunCycleAsync(ct);

        Assert.Single(metrics.Samples);
        Assert.Equal(ServerStatus.Online, servers.All[0].Status);
        Assert.Contains(events.Published, e => e is MetricUpdatedEvent);
        Assert.Contains(events.Published, e => e is ServerStatusChangedEvent);
        Assert.Contains(events.Published, e => e is CollectionCycleCompletedEvent);
    }

    [Fact]
    public async Task RunCycle_HostKeyFailure_MarksHostKeyRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var server = TestData.Server();
        var servers = new FakeServerRepository();
        await servers.AddAsync(server, ct);

        var metrics = new FakeMetricsRepository();
        var events = new RecordingEventBus();
        var settings = new FakeSettingsRepository
        {
            Snapshot = SettingsSnapshot.Default with { CollectionRetryCount = 1 },
        };

        var factory = new FakeSshConnectionFactory((_, _) =>
            throw new HostKeyVerificationException("10.0.1.10:22", "PRESENTED", knownFingerprint: null));

        using var coordinator = CreateCoordinator(
            servers,
            settings,
            factory,
            metrics,
            new FakeAlertRuleRepository(),
            new FakeIncidentRepository(),
            events,
            clock);

        await coordinator.RunCycleAsync(ct);

        Assert.Empty(metrics.Samples);
        Assert.Equal(ServerStatus.HostKeyRejected, servers.All[0].Status);
        Assert.Contains(
            events.Published,
            e => e is ServerStatusChangedEvent changed && changed.Current == ServerStatus.HostKeyRejected);
    }

    [Fact]
    public async Task RunCycle_GatewayFailure_MarksGatewayUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var server = TestData.Server(jumpHost: TestData.JumpHost());
        var servers = new FakeServerRepository();
        await servers.AddAsync(server, ct);

        var settings = new FakeSettingsRepository
        {
            Snapshot = SettingsSnapshot.Default with { CollectionRetryCount = 1 },
        };
        var events = new RecordingEventBus();

        var factory = new FakeSshConnectionFactory((_, _) =>
            throw new GatewayUnavailableException(
                "10.0.0.1",
                "10.0.1.10",
                new InvalidOperationException("bastion down")));

        using var coordinator = CreateCoordinator(
            servers,
            settings,
            factory,
            new FakeMetricsRepository(),
            new FakeAlertRuleRepository(),
            new FakeIncidentRepository(),
            events,
            clock);

        await coordinator.RunCycleAsync(ct);

        Assert.Equal(ServerStatus.GatewayUnavailable, servers.All[0].Status);
    }

    [Fact]
    public async Task RunCycle_RespectsMonitoringInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var server = TestData.Server();
        var servers = new FakeServerRepository();
        await servers.AddAsync(server, ct);

        var metrics = new FakeMetricsRepository();
        var connectCalls = 0;
        var factory = new FakeSshConnectionFactory((_, _) =>
        {
            connectCalls++;
            return Task.FromResult<ISshConnection>(CreateConnection(server.Address.Value));
        });

        using var coordinator = CreateCoordinator(
            servers,
            new FakeSettingsRepository(),
            factory,
            metrics,
            new FakeAlertRuleRepository(),
            new FakeIncidentRepository(),
            new RecordingEventBus(),
            clock);

        await coordinator.RunCycleAsync(ct);
        await coordinator.RunCycleAsync(ct);

        Assert.Equal(1, connectCalls);

        clock.Advance(TimeSpan.FromSeconds(61));
        await coordinator.RunCycleAsync(ct);

        Assert.Equal(2, connectCalls);
    }

    private static FleetMonitoringCoordinator CreateCoordinator(
        IServerRepository servers,
        ISettingsRepository settings,
        ISshConnectionFactory ssh,
        IMetricsRepository metrics,
        IAlertRuleRepository alerts,
        IIncidentRepository incidents,
        IMonitoringEventBus events,
        IClock clock)
    {
        var collector = new LinuxMetricCollector(clock);
        var engine = new IncidentEvaluationEngine(alerts, incidents, events, clock);
        return new FleetMonitoringCoordinator(
            servers,
            settings,
            ssh,
            metrics,
            collector,
            engine,
            events,
            clock,
            NullLogger<FleetMonitoringCoordinator>.Instance);
    }

    private static FakeSshConnection CreateConnection(string host) =>
        new(
            host,
            TimeSpan.FromMilliseconds(12),
            command => command switch
            {
                SshCommands.ReadCpuStat => Ok(CpuStat),
                SshCommands.ReadMemInfo => Ok(MemInfo),
                SshCommands.ReadLoadAverage => Ok("0.50 0.40 0.30 1/200 12345"),
                SshCommands.ReadNetworkDev => Ok(
                    """
                    Inter-|   Receive                                                Transmit
                     face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
                        lo: 1000 10 0 0 0 0 0 0 1000 10 0 0 0 0 0 0
                      eth0: 2000000 1000 0 0 0 0 0 0 1000000 800 0 0 0 0 0 0
                    """),
                SshCommands.ReadUptime => Ok("12345.67 23456.78"),
                SshCommands.ReadDiskUsage => Ok(
                    """
                    Filesystem     1024-blocks      Used Available Capacity Mounted on
                    /dev/sda1         10000000   4000000   6000000      40% /
                    """),
                _ => Ok(string.Empty),
            });

    private static CommandResult Ok(string stdout) => new()
    {
        StandardOutput = stdout,
        StandardError = string.Empty,
        ExitCode = 0,
        Duration = TimeSpan.FromMilliseconds(1),
    };
}
