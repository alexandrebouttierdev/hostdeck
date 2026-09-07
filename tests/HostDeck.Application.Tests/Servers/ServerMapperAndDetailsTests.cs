using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Servers;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using Xunit;

namespace HostDeck.Application.Tests.Servers;

public sealed class ServerMapperAndDetailsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ToSummary_IncludesOperatingSystemFromIdentity()
    {
        var server = CreateServer();
        server.UpdateIdentity(new SystemIdentity(
            "web-front-01",
            "Ubuntu 22.04.4 LTS",
            "5.15.0-105-generic"));

        var summary = ServerMapper.ToSummary(server);

        Assert.Equal("Ubuntu 22.04.4 LTS", summary.OperatingSystem);
    }

    [Fact]
    public void ToSummary_LeavesOperatingSystemNullWithoutIdentity()
    {
        var summary = ServerMapper.ToSummary(CreateServer());

        Assert.Null(summary.OperatingSystem);
    }

    [Fact]
    public async Task GetServerDetails_ExposesHostnameAndKernel()
    {
        var servers = new FakeServerRepository();
        var metrics = new FakeMetricsRepository();
        var incidents = new FakeIncidentRepository();
        var useCase = new GetServerDetailsUseCase(servers, metrics, incidents);

        var server = CreateServer();
        server.UpdateIdentity(new SystemIdentity(
            "web-front-01",
            "Ubuntu 22.04.4 LTS",
            "5.15.0-105-generic"));
        servers.Seed(server);

        var details = await useCase.ExecuteAsync(server.Id.Value, Ct);

        Assert.Equal("web-front-01", details.Hostname);
        Assert.Equal("5.15.0-105-generic", details.KernelVersion);
        Assert.Equal("Ubuntu 22.04.4 LTS", details.Summary.OperatingSystem);
    }

    private static Server CreateServer()
    {
        var id = ServerId.New();
        return new Server(
            id,
            new ServerName("web-front-01"),
            HostAddress.Parse("10.0.1.10"),
            Port.DefaultSsh,
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default);
    }
}
