using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Ports;
using HostDeck.Infrastructure.Monitoring;
using HostDeck.Infrastructure.Ssh;
using HostDeck.Infrastructure.Tests.Fakes;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Monitoring;

public sealed class LinuxMetricCollectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CollectAsync_FirstCycle_DoubleCpuSampleYieldsNonZeroUsage()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var collector = new LinuxMetricCollector(clock);
        var server = TestData.Server();
        var state = new ServerCollectionState();

        var cpuCalls = 0;
        await using var connection = new FakeSshConnection(
            server.Address.Value,
            TimeSpan.FromMilliseconds(5),
            command => command switch
            {
                SshCommands.ReadCpuStat => Ok(NextCpuStat(ref cpuCalls)),
                SshCommands.ReadMemInfo => Ok(MemInfo),
                SshCommands.ReadLoadAverage => Ok("0.50 0.40 0.30 1/200 12345"),
                SshCommands.ReadNetworkDev => Ok(NetworkDev),
                SshCommands.ReadUptime => Ok("12345.67 23456.78"),
                SshCommands.ReadDiskUsage => Ok(DiskUsage),
                SshCommands.ReadOsRelease => Ok(OsRelease),
                SshCommands.ReadHostname => Ok("web-front-01"),
                SshCommands.ReadKernel => Ok("5.15.0-105-generic"),
                _ => throw new InvalidOperationException($"unexpected command: {command}"),
            });

        var success = await collector.CollectAsync(server, connection, state, Ct);

        Assert.Equal(2, cpuCalls);
        Assert.True(success.Sample.Cpu.Total.Value > 0d);
        Assert.NotNull(state.PreviousCpu);
        Assert.Equal("Ubuntu 22.04.4 LTS", success.Identity.OperatingSystem);
        Assert.Equal("web-front-01", success.Identity.Hostname);
        Assert.Equal("5.15.0-105-generic", success.Identity.KernelVersion);
    }

    [Fact]
    public async Task CollectAsync_SubsequentCycle_UsesPreviousCpuWithSingleRead()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));
        var collector = new LinuxMetricCollector(clock);
        var server = TestData.Server();
        var state = new ServerCollectionState();

        var cpuCalls = 0;
        FakeSshConnection CreateConnection() => new(
            server.Address.Value,
            TimeSpan.FromMilliseconds(5),
            command => command switch
            {
                SshCommands.ReadCpuStat => Ok(NextCpuStat(ref cpuCalls)),
                SshCommands.ReadMemInfo => Ok(MemInfo),
                SshCommands.ReadLoadAverage => Ok("0.50 0.40 0.30 1/200 12345"),
                SshCommands.ReadNetworkDev => Ok(NetworkDev),
                SshCommands.ReadUptime => Ok("12345.67 23456.78"),
                SshCommands.ReadDiskUsage => Ok(DiskUsage),
                SshCommands.ReadOsRelease => Ok(OsRelease),
                SshCommands.ReadHostname => Ok("web-front-01"),
                SshCommands.ReadKernel => Ok("5.15.0-105-generic"),
                _ => throw new InvalidOperationException($"unexpected command: {command}"),
            });

        await using (var firstConnection = CreateConnection())
        {
            await collector.CollectAsync(server, firstConnection, state, Ct);
        }

        var cpuAfterFirst = cpuCalls;

        clock.Advance(TimeSpan.FromSeconds(60));
        await using var secondConnection = CreateConnection();
        var second = await collector.CollectAsync(server, secondConnection, state, Ct);

        Assert.Equal(cpuAfterFirst + 1, cpuCalls);
        Assert.True(second.Sample.Cpu.Total.Value > 0d);
    }

    private static string NextCpuStat(ref int call)
    {
        // Chaque appel avance les compteurs pour produire un delta non nul.
        var baseIdle = 4000 + (call * 100);
        var baseUser = 1000 + (call * 400);
        call++;
        return $"""
            cpu  {baseUser} 200 300 {baseIdle} 50 10 20 30 0 0
            cpu0 {baseUser / 2} 100 150 {baseIdle / 2} 25 5 10 15 0 0
            cpu1 {baseUser / 2} 100 150 {baseIdle / 2} 25 5 10 15 0 0
            """;
    }

    private static CommandResult Ok(string stdout) => new()
    {
        StandardOutput = stdout,
        StandardError = string.Empty,
        ExitCode = 0,
        Duration = TimeSpan.FromMilliseconds(1),
    };

    private const string MemInfo = """
        MemTotal:        8000000 kB
        MemFree:         2000000 kB
        MemAvailable:    4000000 kB
        Buffers:          200000 kB
        Cached:          1500000 kB
        SwapTotal:       1000000 kB
        SwapFree:         800000 kB
        """;

    private const string NetworkDev = """
        Inter-|   Receive                                                Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
            lo: 1000 10 0 0 0 0 0 0 1000 10 0 0 0 0 0 0
          eth0: 2000000 1000 0 0 0 0 0 0 1000000 800 0 0 0 0 0 0
        """;

    private const string DiskUsage = """
        Filesystem     1024-blocks      Used Available Capacity Mounted on
        /dev/sda1         10000000   4000000   6000000      40% /
        """;

    private const string OsRelease = """
        NAME="Ubuntu"
        VERSION="22.04.4 LTS"
        ID=ubuntu
        PRETTY_NAME="Ubuntu 22.04.4 LTS"
        """;
}
