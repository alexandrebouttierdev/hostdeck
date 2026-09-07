using HostDeck.Application.Errors;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class CpuStatParserTests
{
    [Fact]
    public void Parse_ReadsAggregateCountersAndCoreCount()
    {
        var raw =
            """
            cpu  1000 200 300 4000 50 10 20 30 0 0
            cpu0 100 20 30 400 5 1 2 3 0 0
            cpu1 900 180 270 3600 45 9 18 27 0 0
            intr 123456 123456
            ctxt 789
            btime 1690000000
            """;

        var times = CpuStatParser.Parse(raw);

        Assert.Equal(1000, times.User);
        Assert.Equal(200, times.Nice);
        Assert.Equal(300, times.System);
        Assert.Equal(4000, times.Idle);
        Assert.Equal(50, times.IoWait);
        Assert.Equal(10, times.Irq);
        Assert.Equal(20, times.SoftIrq);
        Assert.Equal(30, times.Steal);
        Assert.Equal(2, times.CoreCount);
        Assert.Equal(1000 + 200 + 300 + 4000 + 50 + 10 + 20 + 30, times.Total);
    }

    [Fact]
    public void Parse_IgnoresLinesThatDoNotLookLikeCpuCounters()
    {
        var raw =
            """
            process_line_cpu 1 2 3
            cpu  100 20 30 400 5 1 2 3 0 0
            """;

        var times = CpuStatParser.Parse(raw);

        Assert.Equal(100, times.User);
        Assert.Equal(0, times.CoreCount);
    }

    [Fact]
    public void UsageSince_ComputesModePercentages()
    {
        var previous = CpuStatParser.Parse("cpu  0 0 0 0 0 0 0 0 0 0");
        var current = CpuStatParser.Parse(
            """
            cpu  250 50 100 600 0 0 0 0 0 0
            cpu0 250 50 100 600 0 0 0 0 0 0
            cpu1 250 50 100 600 0 0 0 0 0 0
            """);

        var usage = current.UsageSince(previous);

        Assert.Equal(2, usage.CoreCount);
        Assert.Equal(25d, usage.User.Value, precision: 6);
        Assert.Equal(10d, usage.System.Value, precision: 6);
        Assert.Equal(5d, usage.Nice.Value, precision: 6);
        Assert.Equal(0d, usage.IoWait.Value, precision: 6);
        Assert.Equal(0d, usage.Steal.Value, precision: 6);
        Assert.Equal(40d, usage.Total.Value, precision: 6);
    }

    [Fact]
    public void UsageSince_SameCounters_YieldsZeroUsage()
    {
        var raw = "cpu  100 20 30 400 5 1 2 3 0 0";
        var first = CpuStatParser.Parse(raw);
        var second = CpuStatParser.Parse(raw);

        var usage = second.UsageSince(first);

        Assert.Equal(0d, usage.User.Value);
        Assert.Equal(0d, usage.Total.Value);
    }

    [Fact]
    public void UsageSince_CounterRollback_YieldsZeroUsage()
    {
        var previous = CpuStatParser.Parse("cpu  1000 200 300 4000 50 10 20 30 0 0");
        // Après un redémarrage, les compteurs repartent de zéro : le total « recule ».
        var afterReboot = CpuStatParser.Parse("cpu  100 20 30 400 5 1 2 3 0 0");

        var usage = afterReboot.UsageSince(previous);

        Assert.Equal(0d, usage.Total.Value);
    }

    [Fact]
    public void Parse_MissingAggregateLine_Throws()
    {
        var raw =
            """
            cpu0 100 20 30 400 5 1 2 3 0 0
            intr 1 2
            """;

        Assert.Throws<MetricParseException>(() => CpuStatParser.Parse(raw));
    }

    [Fact]
    public void Parse_TooFewCountersOnAggregateLine_Throws()
    {
        Assert.Throws<MetricParseException>(() => CpuStatParser.Parse("cpu 1 2 3"));
    }
}
