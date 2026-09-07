using System;
using System.Linq;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class ServerRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task AddThenGetAll_RoundTripsConfigurationAndTags()
    {
        var server = TestData.Server(group: new ServerGroup("Frontend"));
        server.AddTag(new ServerTag("prod"));
        server.AddTag(new ServerTag("web"));

        await Servers.AddAsync(server, Ct);

        var all = await Servers.GetAllAsync(Ct);
        var reloaded = Assert.Single(all);

        Assert.Equal(server.Id, reloaded.Id);
        Assert.Equal(server.Name.Value, reloaded.Name.Value);
        Assert.Equal(server.Address.Value, reloaded.Address.Value);
        Assert.Equal(server.Port.Value, reloaded.Port.Value);
        Assert.Equal(server.Username.Value, reloaded.Username.Value);
        Assert.Equal(server.Credential.Key, reloaded.Credential.Key);
        Assert.Equal(server.Credential.Kind, reloaded.Credential.Kind);
        Assert.Equal(server.DockerEnabled, reloaded.DockerEnabled);
        Assert.Equal(server.Status, reloaded.Status);
        Assert.NotNull(reloaded.Group);
        Assert.Equal("Frontend", reloaded.Group!.Name);
        Assert.Equal(2, reloaded.Tags.Length);
        Assert.Contains(reloaded.Tags, tag => tag.Value == "prod");
        Assert.Contains(reloaded.Tags, tag => tag.Value == "web");
    }

    [Fact]
    public async Task AddThenGetAll_WithJumpHost_PreservesBastion()
    {
        var server = TestData.Server(jumpHost: TestData.JumpHost());

        await Servers.AddAsync(server, Ct);

        var reloaded = Assert.Single(await Servers.GetAllAsync(Ct));

        Assert.Equal(ConnectionMode.JumpHost, reloaded.ConnectionMode);
        Assert.NotNull(reloaded.JumpHost);
        Assert.Equal("10.0.0.1", reloaded.JumpHost!.Address.Value);
        Assert.Equal(2222, reloaded.JumpHost.Port.Value);
        Assert.Equal("bastion", reloaded.JumpHost.Username.Value);
        Assert.Equal("hostdeck:bastion:1", reloaded.JumpHost.Credential.Key);
    }

    [Fact]
    public async Task Find_ReturnsNullForUnknownServer()
    {
        Assert.Null(await Servers.FindAsync(ServerId.New(), Ct));
    }

    [Fact]
    public async Task Update_PersistsRenameEndpointAndTagReplacement()
    {
        var server = TestData.Server();
        server.AddTag(new ServerTag("old"));
        await Servers.AddAsync(server, Ct);

        server.Rename(new ServerName("web-front-02"));
        server.ChangeEndpoint(
            HostAddress.Parse("10.0.1.11"),
            new Port(2200),
            new SshUsername("deploy"));
        server.ReplaceTags([new ServerTag("new")]);
        await Servers.UpdateAsync(server, Ct);

        var reloaded = await Servers.FindAsync(server.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal("web-front-02", reloaded!.Name.Value);
        Assert.Equal("10.0.1.11", reloaded.Address.Value);
        Assert.Equal(2200, reloaded.Port.Value);
        Assert.Equal("deploy", reloaded.Username.Value);
        var tag = Assert.Single(reloaded.Tags);
        Assert.Equal("new", tag.Value);
    }

    [Fact]
    public async Task Delete_RemovesServerAndItsTags()
    {
        var server = TestData.Server();
        server.AddTag(new ServerTag("prod"));
        await Servers.AddAsync(server, Ct);

        await Servers.DeleteAsync(server.Id, Ct);

        Assert.Empty(await Servers.GetAllAsync(Ct));
        Assert.Null(await Servers.FindAsync(server.Id, Ct));
    }

    [Fact]
    public async Task Delete_OnUnknownServer_Throws()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => Servers.DeleteAsync(ServerId.New(), Ct));
    }

    [Fact]
    public async Task ExistsWithEndpoint_DetectsDuplicatesAndHonorsExclusion()
    {
        var server = TestData.Server(address: "10.0.1.10");
        await Servers.AddAsync(server, Ct);

        // L'adresse enregistrée est détectée…
        Assert.True(await Servers.ExistsWithEndpointAsync(
            HostAddress.Parse("10.0.1.10"),
            new Port(22),
            null,
            Ct));

        // Un autre port sur la même adresse n'est pas un doublon.
        Assert.False(await Servers.ExistsWithEndpointAsync(
            HostAddress.Parse("10.0.1.10"),
            new Port(2222),
            null,
            Ct));

        // Une autre adresse n'est pas un doublon.
        Assert.False(await Servers.ExistsWithEndpointAsync(
            HostAddress.Parse("10.0.1.99"),
            new Port(22),
            null,
            Ct));

        // L'exclusion du serveur lui-même autorise la réécriture de sa propre adresse :
        // c'est le chemin emprunté par le formulaire d'édition.
        Assert.False(await Servers.ExistsWithEndpointAsync(
            HostAddress.Parse("10.0.1.10"),
            new Port(22),
            server.Id,
            Ct));
    }

    [Fact]
    public async Task RecordCollection_IsPersisted()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);

        var collectedAt = new DateTimeOffset(2026, 9, 6, 10, 5, 0, TimeSpan.Zero);
        server.RecordCollection(ServerStatus.Online, collectedAt);
        await Servers.UpdateAsync(server, Ct);

        var reloaded = await Servers.FindAsync(server.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal(ServerStatus.Online, reloaded!.Status);
        Assert.Equal(collectedAt, reloaded.LastCollectedAt);
    }

    [Fact]
    public async Task AddThenGetAll_WithIdentity_RoundTripsSystemIdentity()
    {
        var server = TestData.Server();
        server.UpdateIdentity(new SystemIdentity(
            "web-front-01",
            "Ubuntu 22.04.4 LTS",
            "5.15.0-105-generic"));
        server.RecordCollection(ServerStatus.Online, new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero));

        await Servers.AddAsync(server, Ct);

        var reloaded = Assert.Single(await Servers.GetAllAsync(Ct));
        Assert.NotNull(reloaded.Identity);
        Assert.Equal("web-front-01", reloaded.Identity!.Hostname);
        Assert.Equal("Ubuntu 22.04.4 LTS", reloaded.Identity.OperatingSystem);
        Assert.Equal("5.15.0-105-generic", reloaded.Identity.KernelVersion);
    }
}
