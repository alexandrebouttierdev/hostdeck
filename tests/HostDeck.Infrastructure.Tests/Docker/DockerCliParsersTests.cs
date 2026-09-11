using HostDeck.Application.Errors;
using HostDeck.Domain.Docker;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Docker;
using Xunit;

namespace HostDeck.Infrastructure.Tests.Docker;

public sealed class DockerCliParsersTests
{
    private static readonly ServerId ServerId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

    private const string InfoJson =
        """
        {
          "ServerVersion": "24.0.7",
          "ContainersRunning": 3,
          "Containers": 5,
          "Images": 12
        }
        """;

    private const string PsLine =
        """
        {"ID":"abc123def4567890abcd","Names":"/web-front","Image":"nginx:1.25","State":"running","Status":"Up 2 hours (healthy)","Ports":"0.0.0.0:8080->80/tcp"}
        """;

    private const string StatsLine =
        """
        {"BlockIO":"1.2MB / 640kB","CPUPerc":"12.50%","Container":"web-front","ID":"abc123def4567890abcd","MemPerc":"45.00%","MemUsage":"512MiB / 1GiB","Name":"web-front","NetIO":"1.2MB / 640kB","PIDs":"12"}
        """;

    private const string InspectJson =
        """
        {
          "Name": "/web-front",
          "Image": "sha256:deadbeef",
          "Created": "2024-05-01T10:00:00.123456789Z",
          "State": {
            "Status": "running",
            "StartedAt": "2024-05-01T10:05:00.123456789Z",
            "RestartCount": 2,
            "Health": { "Status": "healthy" }
          },
          "Config": { "Cmd": ["nginx", "-g", "daemon off;"] },
          "NetworkSettings": {
            "Ports": {
              "80/tcp": [{ "HostIp": "0.0.0.0", "HostPort": "8080" }]
            },
            "Networks": { "bridge": {} }
          },
          "Mounts": [{ "Name": "web-data", "Type": "volume" }]
        }
        """;

    [Fact]
    public void ParseInfo_MapsEngineCounters()
    {
        var host = DockerCliParsers.ParseInfo(InfoJson, ServerId);

        Assert.Equal("24.0.7", host.EngineVersion);
        Assert.Equal(3, host.RunningContainers);
        Assert.Equal(5, host.TotalContainers);
        Assert.Equal(12, host.Images);
    }

    [Fact]
    public void ParseContainerList_MapsPsJsonLine()
    {
        var containers = DockerCliParsers.ParseContainerList(PsLine, ServerId);

        Assert.Single(containers);
        var container = containers[0];
        Assert.Equal("abc123def4567890abcd", container.Id.Value);
        Assert.Equal("web-front", container.Name);
        Assert.Equal("nginx:1.25", container.Image);
        Assert.Equal(DockerContainerStatus.Running, container.Status);
        Assert.Equal(DockerHealthStatus.Healthy, container.Health);
        Assert.Single(container.Ports);
    }

    [Fact]
    public void ParseStats_MapsCpuMemoryAndIo()
    {
        var stats = DockerCliParsers.ParseStats(
            StatsLine,
            new DockerContainerId("abc123def4567890abcd"));

        Assert.Equal(12.5d, stats.Cpu.Value, precision: 2);
        Assert.True(stats.MemoryUsed.Bytes > 0);
        Assert.True(stats.MemoryLimit.Bytes > stats.MemoryUsed.Bytes);
        Assert.True(stats.NetworkReceived.Bytes > 0);
        Assert.True(stats.BlockRead.Bytes > 0);
    }

    [Fact]
    public void ParseStatsBatch_IndexByContainerId()
    {
        var batch = DockerCliParsers.ParseStatsBatch(StatsLine);

        Assert.True(batch.ContainsKey("abc123def4567890abcd"));
        Assert.Equal(50d, batch["abc123def4567890abcd"].MemoryPercent, precision: 1);
    }

    [Fact]
    public void ParseInspect_MapsSummaryAndDetails()
    {
        var details = DockerCliParsers.ParseInspect(
            InspectJson,
            ServerId,
            new DockerContainerId("abc123def4567890abcd"));

        Assert.Equal("web-front", details.Summary.Name);
        Assert.Equal(DockerHealthStatus.Healthy, details.Summary.Health);
        Assert.Equal(2, details.Summary.RestartCount);
        Assert.Equal("nginx -g daemon off;", details.Command);
        Assert.Single(details.Ports);
        Assert.Single(details.Networks);
        Assert.Single(details.Volumes);
        Assert.NotNull(details.CreatedAt);
    }

    [Fact]
    public void ParseLogs_ParsesTimestampPrefix()
    {
        const string raw =
            """
            2024-05-01T10:05:01.123456789Z GET /health HTTP/1.1
            plain line without timestamp
            """;

        var entries = DockerCliParsers.ParseLogs(raw).ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal("GET /health HTTP/1.1", entries[0].Message);
        Assert.Equal("plain line without timestamp", entries[1].Message);
    }

    [Fact]
    public void ParseInfo_InvalidJson_ThrowsDockerException()
    {
        Assert.Throws<DockerException>(() => DockerCliParsers.ParseInfo("{not-json", ServerId));
    }

    [Fact]
    public void RequireValidContainerId_RejectsInjection()
    {
        Assert.Throws<DockerException>(() =>
            DockerCliCommands.RequireValidContainerId("abc; rm -rf /"));
    }

    [Fact]
    public void RequireValidContainerId_AcceptsShortHexId()
    {
        var id = DockerCliCommands.RequireValidContainerId("abc123def456");
        Assert.Equal("abc123def456", id);
    }
}
