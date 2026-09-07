using HostDeck.Application.Errors;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class UptimeParserTests
{
    [Fact]
    public void Parse_ReadsFirstFieldAsSeconds()
    {
        var uptime = UptimeParser.Parse("12345.67 9876.54\n");

        Assert.Equal(12345.67, uptime.Value.TotalSeconds, precision: 6);
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        Assert.Throws<MetricParseException>(() => UptimeParser.Parse(""));
    }

    [Fact]
    public void Parse_NegativeInput_Throws()
    {
        Assert.Throws<MetricParseException>(() => UptimeParser.Parse("-1.0"));
    }
}
