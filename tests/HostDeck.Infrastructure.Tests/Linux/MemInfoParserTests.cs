using HostDeck.Application.Errors;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class MemInfoParserTests
{
    private const long KiB = 1024;

    [Fact]
    public void Parse_ComputesUsedExcludingCacheAndBuffers()
    {
        var raw =
            """
            MemTotal:       100000 kB
            MemFree:         20000 kB
            MemAvailable:    32000 kB
            Buffers:         10000 kB
            Cached:          30000 kB
            SwapCached:          0 kB
            SwapTotal:       50000 kB
            SwapFree:        10000 kB
            HugePages_Total:       0
            """;

        var snapshot = MemInfoParser.Parse(raw);

        Assert.Equal(100000 * KiB, snapshot.Memory.Total.Bytes);
        Assert.Equal(40000 * KiB, snapshot.Memory.Used.Bytes);
        Assert.Equal(30000 * KiB, snapshot.Memory.Cached.Bytes);
        Assert.Equal(10000 * KiB, snapshot.Memory.Buffers.Bytes);

        Assert.True(snapshot.Swap.IsConfigured);
        Assert.Equal(50000 * KiB, snapshot.Swap.Total.Bytes);
        Assert.Equal(40000 * KiB, snapshot.Swap.Used.Bytes);
    }

    [Fact]
    public void Parse_WithoutSwap_ReturnsNoSwap()
    {
        var raw =
            """
            MemTotal:        100000 kB
            MemFree:          20000 kB
            Buffers:          10000 kB
            Cached:           30000 kB
            """;

        var snapshot = MemInfoParser.Parse(raw);

        Assert.False(snapshot.Swap.IsConfigured);
        Assert.Equal(0, snapshot.Swap.Total.Bytes);
    }

    [Fact]
    public void Parse_ClampsUsedAtZeroWhenCacheConsumesEverything()
    {
        var raw =
            """
            MemTotal:        1000 kB
            MemFree:          100 kB
            Buffers:          400 kB
            Cached:           600 kB
            """;

        var snapshot = MemInfoParser.Parse(raw);

        Assert.Equal(0, snapshot.Memory.Used.Bytes);
        Assert.Equal(1000 * KiB, snapshot.Memory.Total.Bytes);
    }

    [Fact]
    public void Parse_MissingMemTotal_Throws()
    {
        var raw =
            """
            MemFree: 20000 kB
            Cached:  30000 kB
            """;

        Assert.Throws<MetricParseException>(() => MemInfoParser.Parse(raw));
    }

    [Fact]
    public void Parse_IgnoresUnreadableLines()
    {
        var raw =
            """
            MemTotal:       100000 kB
            Une ligne sans deux-points
            Cached:         30000 kB
            """;

        var snapshot = MemInfoParser.Parse(raw);

        Assert.Equal(100000 * KiB, snapshot.Memory.Total.Bytes);
        Assert.Equal(30000 * KiB, snapshot.Memory.Cached.Bytes);
    }
}
