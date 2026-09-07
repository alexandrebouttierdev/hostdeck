using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Errors;
using HostDeck.Application.Servers;
using HostDeck.Application.Servers.Validation;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Application.Tests.Servers;

public sealed class UpdateServerUseCaseTests
{
    private readonly FakeServerRepository _servers = new();
    private readonly UpdateServerUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UpdateServerUseCaseTests()
    {
        _useCase = new UpdateServerUseCase(
            _servers,
            new UpdateServerDtoValidator(),
            NullLogger<UpdateServerUseCase>.Instance);
    }

    private static Server ExistingServer(
        string address = "10.0.1.10",
        JumpHost? jumpHost = null,
        ServerId? id = null)
    {
        var serverId = id ?? ServerId.New();
        return new Server(
            serverId,
            new ServerName("web-front-01"),
            HostAddress.Parse(address),
            Port.DefaultSsh,
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(serverId, CredentialKind.PrivateKey),
            MonitoringInterval.Default,
            jumpHost);
    }

    private static UpdateServerDto Request(Server server, string address = "10.0.1.11") => new()
    {
        ServerId = server.Id.Value,
        Name = "web-front-01-renamed",
        Address = address,
        Port = 22,
        Username = "hostdeck",
        MonitoringIntervalSeconds = 120,
    };

    [Fact]
    public async Task UpdatesTheServer()
    {
        var server = ExistingServer();
        _servers.Seed(server);

        var summary = await _useCase.ExecuteAsync(Request(server), Ct);

        Assert.Equal("web-front-01-renamed", summary.Name);
        Assert.Equal("10.0.1.11", summary.Address);
        Assert.Equal(1, _servers.UpdateCalls);
    }

    [Fact]
    public async Task FailsWhenTheServerDoesNotExist()
    {
        var absent = ExistingServer();

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _useCase.ExecuteAsync(Request(absent), Ct));
    }

    [Fact]
    public async Task RejectsMovingOntoAnotherServersEndpoint()
    {
        var first = ExistingServer("10.0.1.10");
        var second = ExistingServer("10.0.1.11");
        _servers.Seed(first, second);

        await Assert.ThrowsAsync<ValidationException>(
            () => _useCase.ExecuteAsync(Request(first, "10.0.1.11"), Ct));
    }

    [Fact]
    public async Task AllowsKeepingItsOwnEndpoint()
    {
        var server = ExistingServer("10.0.1.10");
        _servers.Seed(server);

        var summary = await _useCase.ExecuteAsync(Request(server, "10.0.1.10"), Ct);

        Assert.Equal("10.0.1.10", summary.Address);
    }

    /// <summary>
    /// Cas que l'ordre des mutations doit permettre : déplacer un serveur sur l'ancienne
    /// adresse de son bastion tout en le rattachant à un nouveau bastion est légitime.
    /// Un ordre naïf le rejetterait à tort.
    /// </summary>
    [Fact]
    public async Task AllowsMovingOntoTheOldJumpHostAddressWhileChangingJumpHost()
    {
        var oldBastion = new JumpHost(
            HostAddress.Parse("10.0.0.1"),
            Port.DefaultSsh,
            new SshUsername("bastion"),
            new CredentialReference("hostdeck:bastion:old", CredentialKind.PrivateKey));

        var server = ExistingServer("10.0.1.10", oldBastion);
        _servers.Seed(server);

        var request = Request(server, "10.0.0.1") with
        {
            JumpHost = new JumpHostDto
            {
                Address = "10.0.0.2",
                Port = 22,
                Username = "bastion",
                CredentialKey = "hostdeck:bastion:new",
            },
        };

        var summary = await _useCase.ExecuteAsync(request, Ct);

        Assert.Equal("10.0.0.1", summary.Address);
        Assert.Equal(ConnectionMode.JumpHost, summary.ConnectionMode);
    }

    [Fact]
    public async Task RejectsAJumpHostThatBecomesTheTarget()
    {
        var server = ExistingServer("10.0.1.10");
        _servers.Seed(server);

        var request = Request(server, "10.0.0.1") with
        {
            JumpHost = new JumpHostDto
            {
                Address = "10.0.0.1",
                Port = 22,
                Username = "bastion",
                CredentialKey = "hostdeck:bastion:1",
            },
        };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    /// <summary>
    /// Une mise à jour de configuration ne doit jamais toucher au secret : le DTO ne le
    /// porte pas et la référence de credential reste celle d'origine (§30).
    /// </summary>
    [Fact]
    public async Task NeverChangesTheCredential()
    {
        var server = ExistingServer();
        var originalKey = server.Credential.Key;
        _servers.Seed(server);

        await _useCase.ExecuteAsync(Request(server), Ct);

        var reloaded = await _servers.FindAsync(server.Id, Ct);
        Assert.Equal(originalKey, reloaded!.Credential.Key);
    }
}

public sealed class DeleteServerUseCaseTests
{
    private readonly FakeServerRepository _servers = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly DeleteServerUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DeleteServerUseCaseTests()
    {
        _useCase = new DeleteServerUseCase(
            _servers,
            _credentials,
            NullLogger<DeleteServerUseCase>.Instance);
    }

    private Server SeedServer()
    {
        var id = ServerId.New();
        var server = new Server(
            id,
            new ServerName("web-front-01"),
            HostAddress.Parse("10.0.1.10"),
            Port.DefaultSsh,
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default);

        _servers.Seed(server);
        return server;
    }

    [Fact]
    public async Task DeletesTheServerAndItsSecret()
    {
        var server = SeedServer();
        await _credentials.WriteAsync(server.Credential, "secret".AsMemory(), Ct);

        await _useCase.ExecuteAsync(server.Id.Value, Ct);

        Assert.Equal(1, _servers.DeleteCalls);
        Assert.Empty(_credentials.Secrets);
    }

    [Fact]
    public async Task FailsWhenTheServerDoesNotExist() =>
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _useCase.ExecuteAsync(Guid.NewGuid(), Ct));

    /// <summary>
    /// Si le trousseau échoue, la suppression du serveur reste acquise : laisser en place un
    /// hôte dont l'identifiant a déjà disparu serait pire qu'une entrée résiduelle signalée.
    /// </summary>
    [Fact]
    public async Task StillDeletesTheServerWhenTheKeychainFails()
    {
        var server = SeedServer();
        _credentials.FailNextDeleteWith = new CredentialException(
            "keychain locked",
            CredentialFailure.StoreUnavailable);

        await _useCase.ExecuteAsync(server.Id.Value, Ct);

        Assert.Equal(1, _servers.DeleteCalls);
        Assert.Null(await _servers.FindAsync(server.Id, Ct));
    }
}

public sealed class GetServersUseCaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReturnsEveryServerWithItsLatestMetrics()
    {
        var servers = new FakeServerRepository();
        var metrics = new FakeMetricsRepository();

        var id = ServerId.New();
        servers.Seed(new Server(
            id,
            new ServerName("web-front-01"),
            HostAddress.Parse("10.0.1.10"),
            Port.DefaultSsh,
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default));

        metrics.SeedLatest(new Dtos.Monitoring.LatestMetricDto
        {
            ServerId = id.Value,
            ObservedAt = DateTimeOffset.UtcNow,
            CpuPercent = 12d,
            MemoryPercent = 38d,
        });

        var result = await new GetServersUseCase(servers, metrics).ExecuteAsync(Ct);

        var summary = Assert.Single(result);
        Assert.Equal(12d, summary.Latest!.CpuPercent);
    }

    /// <summary>
    /// Un hôte jamais collecté doit apparaître dans l'inventaire, sans métriques, plutôt que
    /// d'en être absent : c'est exactement l'état d'un serveur qu'on vient d'ajouter.
    /// </summary>
    [Fact]
    public async Task ListsAServerThatHasNeverBeenCollected()
    {
        var servers = new FakeServerRepository();
        var id = ServerId.New();
        servers.Seed(new Server(
            id,
            new ServerName("new-host"),
            HostAddress.Parse("10.0.9.9"),
            Port.DefaultSsh,
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default));

        var result = await new GetServersUseCase(servers, new FakeMetricsRepository()).ExecuteAsync(Ct);

        var summary = Assert.Single(result);
        Assert.Null(summary.Latest);
        Assert.Equal(ServerStatus.Unknown, summary.Status);
    }

    [Fact]
    public async Task ReturnsAnEmptyListWhenThereIsNoServer()
    {
        var result = await new GetServersUseCase(
            new FakeServerRepository(),
            new FakeMetricsRepository()).ExecuteAsync(Ct);

        Assert.Empty(result);
    }
}
