using System;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Xunit;

namespace HostDeck.Domain.Tests.Servers;

public sealed class ServerTests
{
    [Fact]
    public void NewServerStartsWithUnknownStatusAndNoCollection()
    {
        var server = TestData.Server();

        Assert.Equal(ServerStatus.Unknown, server.Status);
        Assert.Null(server.LastCollectedAt);
    }

    [Fact]
    public void ConnectionModeIsDirectWithoutJumpHost() =>
        Assert.Equal(ConnectionMode.Direct, TestData.Server().ConnectionMode);

    [Fact]
    public void ConnectionModeIsJumpHostWhenBastionConfigured()
    {
        var server = TestData.Server(TestData.JumpHost());

        Assert.Equal(ConnectionMode.JumpHost, server.ConnectionMode);
    }

    /// <summary>
    /// Cas de cycle imposé par le §41 : un bastion qui pointe sur sa propre cible ne fournit
    /// aucun chemin, la connexion tournerait sur elle-même.
    /// </summary>
    [Fact]
    public void RejectsJumpHostPointingAtItsOwnTarget()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            TestData.Server(TestData.JumpHost("10.0.1.10"), address: "10.0.1.10"));

        Assert.Contains("bastion", exception.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsJumpHostOnSameAddressButDifferentPort()
    {
        var server = TestData.Server(TestData.JumpHost("10.0.1.10", 2222), address: "10.0.1.10");

        Assert.Equal(ConnectionMode.JumpHost, server.ConnectionMode);
    }

    [Fact]
    public void RejectsChangingEndpointOntoTheExistingJumpHost()
    {
        var server = TestData.Server(TestData.JumpHost("10.0.0.1"));

        Assert.Throws<DomainValidationException>(() => server.ChangeEndpoint(
            HostAddress.Parse("10.0.0.1"),
            Port.DefaultSsh,
            new SshUsername("hostdeck")));
    }

    [Fact]
    public void RejectsAttachingJumpHostThatIsAlreadyTheTarget()
    {
        var server = TestData.Server(address: "10.0.5.5");

        Assert.Throws<DomainValidationException>(() =>
            server.ChangeJumpHost(TestData.JumpHost("10.0.5.5")));
    }

    [Fact]
    public void RecordCollectionStoresStatusAndInstantInUtc()
    {
        var server = TestData.Server();
        var observedAt = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.FromHours(2));

        server.RecordCollection(ServerStatus.Online, observedAt);

        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(TimeSpan.Zero, server.LastCollectedAt!.Value.Offset);
        Assert.Equal(observedAt.UtcDateTime, server.LastCollectedAt.Value.UtcDateTime);
    }

    [Fact]
    public void RecordCollectionRejectsUnknownAsAnOutcome() =>
        Assert.Throws<DomainValidationException>(() =>
            TestData.Server().RecordCollection(ServerStatus.Unknown, TestData.Now));

    /// <summary>
    /// Un relevé antérieur au précédent ferait reculer l'horodatage de dernière collecte
    /// et rendrait l'inventaire incohérent.
    /// </summary>
    [Fact]
    public void RecordCollectionRejectsAnInstantGoingBackwards()
    {
        var server = TestData.Server();
        server.RecordCollection(ServerStatus.Online, TestData.Now);

        Assert.Throws<DomainValidationException>(() =>
            server.RecordCollection(ServerStatus.Offline, TestData.Now.AddSeconds(-1)));
    }

    [Theory]
    [InlineData(ServerStatus.Online)]
    [InlineData(ServerStatus.Offline)]
    [InlineData(ServerStatus.GatewayUnavailable)]
    [InlineData(ServerStatus.AuthenticationFailed)]
    [InlineData(ServerStatus.HostKeyRejected)]
    public void RecordCollectionAcceptsEveryConclusiveStatus(ServerStatus status)
    {
        var server = TestData.Server();

        server.RecordCollection(status, TestData.Now);

        Assert.Equal(status, server.Status);
    }

    [Fact]
    public void TagsAreDeduplicatedRegardlessOfCase()
    {
        var server = TestData.Server();

        Assert.True(server.AddTag(new ServerTag("prod")));
        Assert.False(server.AddTag(new ServerTag("PROD")));
        Assert.Single(server.Tags);
    }

    [Fact]
    public void ReplaceTagsSwapsTheWholeSet()
    {
        var server = TestData.Server();
        server.AddTag(new ServerTag("prod"));

        server.ReplaceTags([new ServerTag("staging"), new ServerTag("web")]);

        Assert.Equal(2, server.Tags.Length);
        Assert.False(server.HasTag(new ServerTag("prod")));
        Assert.True(server.HasTag(new ServerTag("web")));
    }

    /// <summary>
    /// La collection interne ne doit pas fuir : modifier ce que renvoie Tags ne doit pas
    /// altérer l'agrégat (§61, collections).
    /// </summary>
    [Fact]
    public void TagsAreExposedAsAnImmutableSnapshot()
    {
        var server = TestData.Server();
        server.AddTag(new ServerTag("prod"));

        var snapshot = server.Tags;
        server.AddTag(new ServerTag("web"));

        Assert.Single(snapshot);
        Assert.Equal(2, server.Tags.Length);
    }
}
