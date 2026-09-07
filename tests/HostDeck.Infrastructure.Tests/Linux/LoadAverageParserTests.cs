using HostDeck.Application.Errors;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class LoadAverageParserTests
{
    [Fact]
    public void Parse_ReadsFirstThreeFields()
    {
        var load = LoadAverageParser.Parse("0.52 0.58 0.59 1/267 12345\n");

        Assert.Equal(0.52, load.OneMinute, precision: 6);
        Assert.Equal(0.58, load.FiveMinutes, precision: 6);
        Assert.Equal(0.59, load.FifteenMinutes, precision: 6);
    }

    [Fact]
    public void Parse_TooFewFields_Throws()
    {
        Assert.Throws<MetricParseException>(() => LoadAverageParser.Parse("0.52 0.58"));
    }

    [Fact]
    public void Parse_NonNumericFields_Throws()
    {
        Assert.Throws<MetricParseException>(() => LoadAverageParser.Parse("a b c"));
    }
}
