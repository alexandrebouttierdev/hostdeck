using System;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Domain.Servers;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class MetricsRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task AddBatchThenGetLatest_ReturnsMostRecentSample()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        await Metrics.AddBatchAsync(
        [
            TestData.Sample(server.Id, elapsedMinutes: 10),
            TestData.Sample(server.Id, elapsedMinutes: 11),
        ], Ct);

        var latest = await Metrics.GetLatestAsync(server.Id, Ct);

        Assert.NotNull(latest);
        Assert.Equal(TestData.Epoch.AddMinutes(11), latest!.ObservedAt);
        AssertCpuTotal(30.1, latest.CpuPercent);
        AssertMemoryPercent(11800, 20480, latest.MemoryPercent);
        Assert.Equal(2.91, latest.LoadOneMinute);
        Assert.Equal(TimeSpan.FromSeconds(3600 + 11 * 60), latest.Uptime);
    }

    [Fact]
    public async Task GetLatest_ReturnsNullWithoutSamples()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        Assert.Null(await Metrics.GetLatestAsync(server.Id, Ct));
    }

    [Fact]
    public async Task GetLatestForAll_ReturnsOneRowPerServer()
    {
        var first = TestData.Server(address: "10.0.1.10");
        var second = TestData.Server(address: "10.0.1.11");
        await Servers.AddAsync(first, Ct);
        await Servers.AddAsync(second, Ct);

        await Metrics.AddBatchAsync([TestData.Sample(first.Id, elapsedMinutes: 1)], Ct);
        await Metrics.AddBatchAsync([TestData.Sample(second.Id, elapsedMinutes: 2)], Ct);

        var rows = await Metrics.GetLatestForAllAsync(Ct);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.ServerId == first.Id.Value);
        Assert.Contains(rows, row => row.ServerId == second.Id.Value);
    }

    [Fact]
    public async Task GetLatest_ComputesBusiestDiskPercentage()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        await Metrics.AddBatchAsync(
        [
            TestData.Sample(
                server.Id,
                elapsedMinutes: 1,
                disks:
                [
                    TestData.Disk(mountPoint: "/", totalMib: 100_000, usedMib: 40_000),
                    TestData.Disk(mountPoint: "/var", totalMib: 50_000, usedMib: 35_000),
                ]),
        ], Ct);

        var latest = await Metrics.GetLatestAsync(server.Id, Ct);

        Assert.NotNull(latest);
        // /var est la partition la plus remplie : 70 %.
        Assert.Equal(70d, latest!.DiskPercent);
    }

    [Fact]
    public async Task GetHistory_AggregatesMinAvgMaxInBuckets()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        // Six relevés à une minute d'intervalle. La plage de 5 minutes demandée en 3 points
        // donne un bucket de 2 minutes : chaque bucket agrège deux relevés.
        await Metrics.AddBatchAsync(
        [
            Sample(server, 0, 20d),
            Sample(server, 1, 40d),
            Sample(server, 2, 30d),
            Sample(server, 3, 50d),
            Sample(server, 4, 60d),
            Sample(server, 5, 80d),
        ], Ct);

        var history = await Metrics.GetHistoryAsync(new MetricHistoryRequestDto
        {
            ServerId = server.Id.Value,
            From = TestData.Epoch,
            To = TestData.Epoch.AddMinutes(5),
            Series = [MetricSeriesKind.CpuTotal],
            MaxPoints = 3,
        }, Ct);

        Assert.Equal(TimeSpan.FromMinutes(2), history.BucketSize);
        var series = Assert.Single(history.Series);
        Assert.Equal(MetricSeriesKind.CpuTotal, series.Kind);
        Assert.Equal(3, series.Points.Count);

        AssertBucket(series.Points[0], TestData.Epoch, 20d, 30d, 40d);
        AssertBucket(series.Points[1], TestData.Epoch.AddMinutes(2), 30d, 40d, 50d);
        AssertBucket(series.Points[2], TestData.Epoch.AddMinutes(4), 60d, 70d, 80d);
    }

    [Fact]
    public async Task GetHistory_NetworkSeriesSumsInterfacesThenAggregates()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        await Metrics.AddBatchAsync(
        [
            TestData.Sample(
                server.Id,
                elapsedMinutes: 1,
                interfaces:
                [
                    TestData.Interface("eth0", receivedBytes: 1_000_000, transmittedBytes: 500_000),
                    TestData.Interface("eth1", receivedBytes: 3_000_000, transmittedBytes: 250_000),
                ]),
        ], Ct);

        var history = await Metrics.GetHistoryAsync(new MetricHistoryRequestDto
        {
            ServerId = server.Id.Value,
            From = TestData.Epoch,
            To = TestData.Epoch.AddMinutes(5),
            Series = [MetricSeriesKind.NetworkReceived],
            MaxPoints = 300,
        }, Ct);

        var point = Assert.Single(Assert.Single(history.Series).Points);
        Assert.Equal(4_000_000d, point.Maximum);
        Assert.Equal(4_000_000d, point.Average);
    }

    [Fact]
    public async Task PruneOlderThan_RemovesOnlyRowsBeforeCutoff()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        await Metrics.AddBatchAsync(
        [
            TestData.Sample(server.Id, elapsedMinutes: 0),
            TestData.Sample(server.Id, elapsedMinutes: 1),
            TestData.Sample(server.Id, elapsedMinutes: 2),
            TestData.Sample(server.Id, elapsedMinutes: 3),
            TestData.Sample(server.Id, elapsedMinutes: 4),
        ], Ct);

        var removed = await Metrics.PruneOlderThanAsync(TestData.Epoch.AddMinutes(3), Ct);

        Assert.Equal(3, removed);

        var latest = await Metrics.GetLatestAsync(server.Id, Ct);
        Assert.NotNull(latest);
        Assert.Equal(TestData.Epoch.AddMinutes(4), latest!.ObservedAt);
    }

    private static Domain.Monitoring.MetricSample Sample(
        Server server,
        long minute,
        double cpuTotal) =>
        TestData.Sample(server.Id, minute, cpuTotal);

    private static void AssertBucket(
        MetricPointDto point,
        DateTimeOffset expectedStart,
        double minimum,
        double average,
        double maximum)
    {
        Assert.Equal(expectedStart, point.Timestamp);
        // Le SQL agrège des sommes de modes CPU en REAL : une tolérance absorbe le bruit
        // flottant inhérent aux décompositions binaires.
        Assert.Equal(minimum, point.Minimum, precision: 6);
        Assert.Equal(average, point.Average, precision: 6);
        Assert.Equal(maximum, point.Maximum, precision: 6);
    }

    private static void AssertCpuTotal(double expected, double actual)
    {
        // Les modes valent 18,4 + 7,2 + 4,1 + 0,3 + 0,1 ; le total SQL fait la somme.
        Assert.Equal(expected, actual, precision: 6);
    }

    private static void AssertMemoryPercent(long usedMib, long totalMib, double actual)
    {
        var expected = usedMib * 100.0 / totalMib;
        Assert.Equal(expected, actual, precision: 6);
    }
}
