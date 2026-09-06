using System;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Xunit;

namespace HostDeck.Domain.Tests.Servers;

public sealed class HostAddressTests
{
    [Theory]
    [InlineData("10.0.1.10", HostAddressKind.IPv4)]
    [InlineData("192.168.0.1", HostAddressKind.IPv4)]
    [InlineData("2001:db8::1", HostAddressKind.IPv6)]
    [InlineData("::1", HostAddressKind.IPv6)]
    [InlineData("db-01.prod.lan", HostAddressKind.DnsName)]
    [InlineData("server", HostAddressKind.DnsName)]
    [InlineData("web-front-01.eu-west-1.example.com", HostAddressKind.DnsName)]
    public void ParsesValidAddresses(string value, HostAddressKind expectedKind)
    {
        var address = HostAddress.Parse(value);

        Assert.Equal(expectedKind, address.Kind);
    }

    [Fact]
    public void TrimsSurroundingWhitespace()
    {
        var address = HostAddress.Parse("  db-01.prod.lan  ");

        Assert.Equal("db-01.prod.lan", address.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".leading-dot")]
    [InlineData("trailing-dot.")]
    [InlineData("-starts-with-hyphen")]
    [InlineData("ends-with-hyphen-")]
    [InlineData("double..dot")]
    [InlineData("has space")]
    [InlineData("under_score")]
    public void RejectsMalformedAddresses(string value) =>
        Assert.Throws<DomainValidationException>(() => HostAddress.Parse(value));

    /// <summary>
    /// Ces valeurs sont refusées parce qu'elles atteindraient la couche SSH : accepter un
    /// point-virgule ou un pipe reviendrait à compter sur un échappement en aval (§51, T4).
    /// </summary>
    [Theory]
    [InlineData("host;rm -rf /")]
    [InlineData("host && whoami")]
    [InlineData("host|cat /etc/passwd")]
    [InlineData("host$(id)")]
    [InlineData("host`id`")]
    [InlineData("host\nsecond-line")]
    public void RejectsShellMetacharacters(string value) =>
        Assert.Throws<DomainValidationException>(() => HostAddress.Parse(value));

    [Fact]
    public void RejectsLabelLongerThanSixtyThreeCharacters()
    {
        var tooLong = new string('a', 64);

        Assert.Throws<DomainValidationException>(() => HostAddress.Parse(tooLong));
    }

    [Fact]
    public void TryParseReportsFailureWithoutThrowing()
    {
        Assert.False(HostAddress.TryParse("not valid", out var address));
        Assert.Null(address);
    }
}

public sealed class PortTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(65535)]
    public void AcceptsPortsInRange(int value) => Assert.Equal(value, new Port(value).Value);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void RejectsPortsOutOfRange(int value) =>
        Assert.Throws<DomainValidationException>(() => new Port(value));

    [Fact]
    public void DefaultSshPortIsTwentyTwo() => Assert.Equal(22, Port.DefaultSsh.Value);
}

public sealed class SshUsernameTests
{
    [Theory]
    [InlineData("root")]
    [InlineData("hostdeck")]
    [InlineData("deploy-user")]
    [InlineData("user_1")]
    [InlineData("svc.account")]
    public void AcceptsPosixUsernames(string value) => Assert.Equal(value, new SshUsername(value).Value);

    [Theory]
    [InlineData("")]
    [InlineData("user;id")]
    [InlineData("user name")]
    [InlineData("user@host")]
    [InlineData("-leading-hyphen")]
    [InlineData("user$(id)")]
    public void RejectsUnsafeUsernames(string value) =>
        Assert.Throws<DomainValidationException>(() => new SshUsername(value));
}

public sealed class ServerNameTests
{
    [Fact]
    public void TrimsWhitespace() => Assert.Equal("web-01", new ServerName("  web-01  ").Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsBlankNames(string value) =>
        Assert.Throws<DomainValidationException>(() => new ServerName(value));

    [Fact]
    public void RejectsNamesBeyondMaximumLength()
    {
        var tooLong = new string('a', ServerName.MaxLength + 1);

        Assert.Throws<DomainValidationException>(() => new ServerName(tooLong));
    }
}

public sealed class ServerTagTests
{
    [Fact]
    public void NormalisesToLowercase() => Assert.Equal("prod", new ServerTag("PROD").Value);

    [Fact]
    public void TagsAreComparedByNormalisedValue() =>
        Assert.Equal(new ServerTag("Prod"), new ServerTag("prod"));

    [Theory]
    [InlineData("has space")]
    [InlineData("tag;drop")]
    [InlineData("")]
    public void RejectsInvalidTags(string value) =>
        Assert.Throws<DomainValidationException>(() => new ServerTag(value));
}

public sealed class MonitoringIntervalTests
{
    [Fact]
    public void DefaultIntervalIsSixtySeconds() =>
        Assert.Equal(TimeSpan.FromSeconds(60), MonitoringInterval.Default.Value);

    /// <summary>
    /// La borne basse protège la production supervisée : un intervalle d'une seconde ferait
    /// de HostDeck une source de charge sur les machines qu'il observe (§51, T8).
    /// </summary>
    [Fact]
    public void RejectsIntervalBelowMinimum() =>
        Assert.Throws<DomainValidationException>(() => new MonitoringInterval(TimeSpan.FromSeconds(1)));

    [Fact]
    public void RejectsIntervalAboveMaximum() =>
        Assert.Throws<DomainValidationException>(() => new MonitoringInterval(TimeSpan.FromHours(2)));
}

public sealed class CredentialReferenceTests
{
    /// <summary>
    /// Une référence de credential doit pouvoir apparaître dans un journal sans divulguer
    /// quoi que ce soit (§51, T2). Ce test verrouille cette propriété.
    /// </summary>
    [Fact]
    public void ContainsOnlyAnOpaqueKey()
    {
        var serverId = ServerId.New();

        var reference = CredentialReference.ForServer(serverId, CredentialKind.PrivateKey);

        Assert.Equal(reference.Key, reference.ToString());
        Assert.Contains(serverId.Value.ToString("D"), reference.Key, StringComparison.Ordinal);
        Assert.Equal(CredentialKind.PrivateKey, reference.Kind);
    }
}
