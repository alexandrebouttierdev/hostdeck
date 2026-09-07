using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Linux;
using HostDeck.Infrastructure.Ssh;

namespace HostDeck.Infrastructure.Monitoring;

/// <summary>
/// Assemble parsers Linux purs + session SSH en un <see cref="MetricSample"/>.
/// </summary>
internal sealed class LinuxMetricCollector
{
    /// <summary>
    /// Délai entre les deux relevés <c>/proc/stat</c> du premier cycle : assez long pour
    /// obtenir un delta de ticks non nul, assez court pour rester dans le timeout de collecte.
    /// </summary>
    internal static readonly TimeSpan FirstCycleCpuSampleDelay = TimeSpan.FromMilliseconds(300);

    private readonly IClock _clock;

    public LinuxMetricCollector(IClock clock)
    {
        _clock = clock;
    }

    public async Task<CollectionSuccess> CollectAsync(
        Server server,
        ISshConnection connection,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(state);

        var observedAt = _clock.UtcNow;

        var (cpuTimes, cpu) = await CollectCpuAsync(connection, state, cancellationToken)
            .ConfigureAwait(false);

        var memRaw = await RequireAsync(connection, SshCommands.ReadMemInfo, cancellationToken).ConfigureAwait(false);
        var loadRaw = await RequireAsync(connection, SshCommands.ReadLoadAverage, cancellationToken).ConfigureAwait(false);
        var netRaw = await RequireAsync(connection, SshCommands.ReadNetworkDev, cancellationToken).ConfigureAwait(false);
        var uptimeRaw = await RequireAsync(connection, SshCommands.ReadUptime, cancellationToken).ConfigureAwait(false);
        var diskRaw = await RequireAsync(connection, SshCommands.ReadDiskUsage, cancellationToken).ConfigureAwait(false);

        var identity = await CollectIdentityAsync(connection, cancellationToken).ConfigureAwait(false);

        var memory = MemInfoParser.Parse(memRaw);
        var load = LoadAverageParser.Parse(loadRaw);
        var interfaces = NetworkDevParser.Parse(netRaw);
        var uptime = UptimeParser.Parse(uptimeRaw);
        var disks = DiskUsageParser.Parse(diskRaw);

        var sample = new MetricSample(
            server.Id,
            observedAt,
            cpu,
            memory.Memory,
            memory.Swap,
            load,
            uptime,
            disks,
            interfaces);

        var networkRate = ComputeAggregateNetworkRate(state.PreviousSample, sample);

        state.PreviousCpu = cpuTimes;
        state.PreviousSample = sample;

        var latest = new LatestMetricDto
        {
            ServerId = server.Id.Value,
            ObservedAt = observedAt,
            CpuPercent = cpu.Total.Value,
            MemoryPercent = memory.Memory.UsedRatio.Value,
            DiskPercent = sample.BusiestDisk?.UsedRatio.Value ?? 0d,
            LoadOneMinute = load.OneMinute,
            NetworkReceivedBytesPerSecond = networkRate.ReceivedBytesPerSecond,
            NetworkTransmittedBytesPerSecond = networkRate.TransmittedBytesPerSecond,
            Uptime = uptime.Value,
        };

        return new CollectionSuccess(sample, latest, connection.Latency, identity);
    }

    /// <summary>
    /// Au premier cycle (<see cref="ServerCollectionState.PreviousCpu"/> absent), un double
    /// relevé dans la même collecte fournit un delta réel — sans inventer de pourcentage.
    /// Les cycles suivants comparent au relevé précédent conservé en mémoire.
    /// </summary>
    private static async Task<(CpuTimes Times, CpuUsage Usage)> CollectCpuAsync(
        ISshConnection connection,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        if (state.PreviousCpu is { } previous)
        {
            var cpuRaw = await RequireAsync(connection, SshCommands.ReadCpuStat, cancellationToken)
                .ConfigureAwait(false);
            var cpuTimes = CpuStatParser.Parse(cpuRaw);
            return (cpuTimes, cpuTimes.UsageSince(previous));
        }

        var firstRaw = await RequireAsync(connection, SshCommands.ReadCpuStat, cancellationToken)
            .ConfigureAwait(false);
        var first = CpuStatParser.Parse(firstRaw);

        await Task.Delay(FirstCycleCpuSampleDelay, cancellationToken).ConfigureAwait(false);

        var secondRaw = await RequireAsync(connection, SshCommands.ReadCpuStat, cancellationToken)
            .ConfigureAwait(false);
        var second = CpuStatParser.Parse(secondRaw);

        return (second, second.UsageSince(first));
    }

    private static async Task<SystemIdentity> CollectIdentityAsync(
        ISshConnection connection,
        CancellationToken cancellationToken)
    {
        var osRaw = await RequireAsync(connection, SshCommands.ReadOsRelease, cancellationToken)
            .ConfigureAwait(false);
        var hostnameRaw = await RequireAsync(connection, SshCommands.ReadHostname, cancellationToken)
            .ConfigureAwait(false);
        var kernelRaw = await RequireAsync(connection, SshCommands.ReadKernel, cancellationToken)
            .ConfigureAwait(false);

        var os = OsReleaseParser.Parse(osRaw);

        return new SystemIdentity(
            hostnameRaw,
            os.DisplayName,
            kernelRaw);
    }

    private static async Task<string> RequireAsync(
        ISshConnection connection,
        string command,
        CancellationToken cancellationToken)
    {
        var result = await connection.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new SshCommandException(
                connection.Host,
                command,
                result.ExitCode,
                result.StandardError);
        }

        return result.StandardOutput;
    }

    private static NetworkRate ComputeAggregateNetworkRate(MetricSample? previous, MetricSample current)
    {
        if (previous is null)
        {
            return NetworkRate.Zero("aggregate");
        }

        var elapsed = current.ObservedAt - previous.ObservedAt;
        double rx = 0d;
        double tx = 0d;

        foreach (var iface in current.Interfaces)
        {
            if (iface.InterfaceName is "lo" or "lo0")
            {
                continue;
            }

            var prior = previous.Interfaces.FirstOrDefault(candidate =>
                string.Equals(candidate.InterfaceName, iface.InterfaceName, StringComparison.Ordinal));

            if (prior is null)
            {
                continue;
            }

            var rate = iface.RateSince(prior, elapsed);
            rx += rate.ReceivedBytesPerSecond;
            tx += rate.TransmittedBytesPerSecond;
        }

        return new NetworkRate("aggregate", rx, tx);
    }
}

internal sealed record CollectionSuccess(
    MetricSample Sample,
    LatestMetricDto Latest,
    TimeSpan SshLatency,
    SystemIdentity Identity);
