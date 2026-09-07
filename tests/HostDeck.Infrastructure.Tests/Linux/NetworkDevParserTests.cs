using System.Linq;
using HostDeck.Infrastructure.Linux;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Linux;

public sealed class NetworkDevParserTests
{
    [Fact]
    public void Parse_ReadsReceivedAndTransmittedBytesPerInterface()
    {
        var raw =
            """
            Inter-|   Receive                                                |  Transmit
             face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
                lo: 1234567    1000    0    0    0     0          0         0  7654321    1000    0    0    0     0       0          0
              eth0: 987654321 123456 0 0 0 0 0 0 1122334455 654321 0 0 0 0 0 0
            """;

        var interfaces = NetworkDevParser.Parse(raw);

        Assert.Equal(2, interfaces.Count);

        var loopback = Assert.Single(interfaces, item => item.InterfaceName == "lo");
        Assert.Equal(1_234_567, loopback.Received.Bytes);
        Assert.Equal(7_654_321, loopback.Transmitted.Bytes);

        var ethernet = Assert.Single(interfaces, item => item.InterfaceName == "eth0");
        Assert.Equal(987_654_321, ethernet.Received.Bytes);
        Assert.Equal(1_122_334_455, ethernet.Transmitted.Bytes);
    }

    [Fact]
    public void Parse_SkipsMalformedDataLines()
    {
        var raw =
            """
             face |bytes    packets
                lo: 1234    1000    0    0    0     0          0         0  5678    1000    0    0    0     0       0          0
              bad: notanumber 1000 0 0 0 0 0 0 5 6 7 8 9 10 11 12
            """;

        var interfaces = NetworkDevParser.Parse(raw);

        var item = Assert.Single(interfaces);
        Assert.Equal("lo", item.InterfaceName);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(NetworkDevParser.Parse(""));
    }
}
