using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Docker;

/// <summary>
/// Moteur Docker présent sur un serveur supervisé.
///
/// Modélisé à part du <see cref="Server"/> : Docker peut être absent, arrêté, ou
/// inaccessible alors que le serveur lui-même répond parfaitement.
/// </summary>
public sealed record DockerHost
{
    public const int MaxVersionLength = 64;

    public DockerHost(
        ServerId serverId,
        string engineVersion,
        int runningContainers,
        int totalContainers,
        int images)
    {
        if (runningContainers > totalContainers)
        {
            throw new DomainValidationException(
                nameof(runningContainers),
                "le nombre de conteneurs actifs ne peut pas dépasser le nombre total");
        }

        ServerId = serverId;
        EngineVersion = Guard.RequiredText(engineVersion, MaxVersionLength);
        RunningContainers = Guard.NotNegative(runningContainers);
        TotalContainers = Guard.NotNegative(totalContainers);
        Images = Guard.NotNegative(images);
    }

    public ServerId ServerId { get; }

    public string EngineVersion { get; }

    public int RunningContainers { get; }

    public int TotalContainers { get; }

    public int Images { get; }

    public int StoppedContainers => TotalContainers - RunningContainers;
}

/// <summary>Image présente sur un hôte Docker.</summary>
public sealed record DockerImage
{
    public const int MaxLength = 512;

    public DockerImage(string id, string repositoryTag, ByteSize size)
    {
        Id = Guard.RequiredText(id, MaxLength);
        RepositoryTag = Guard.RequiredText(repositoryTag, MaxLength);
        Size = size;
    }

    public string Id { get; }

    /// <summary>Nom complet « dépôt:tag », par exemple « postgres:15-alpine ».</summary>
    public string RepositoryTag { get; }

    public ByteSize Size { get; }
}

/// <summary>Volume Docker.</summary>
public sealed record DockerVolume
{
    public const int MaxLength = 255;

    public DockerVolume(string name, string driver, string? mountPoint)
    {
        Name = Guard.RequiredText(name, MaxLength);
        Driver = Guard.RequiredText(driver, MaxLength);
        MountPoint = string.IsNullOrWhiteSpace(mountPoint) ? null : mountPoint.Trim();
    }

    public string Name { get; }

    public string Driver { get; }

    public string? MountPoint { get; }
}

/// <summary>Réseau Docker.</summary>
public sealed record DockerNetwork
{
    public const int MaxLength = 255;

    public DockerNetwork(string id, string name, string driver, string? subnet)
    {
        Id = Guard.RequiredText(id, MaxLength);
        Name = Guard.RequiredText(name, MaxLength);
        Driver = Guard.RequiredText(driver, MaxLength);
        Subnet = string.IsNullOrWhiteSpace(subnet) ? null : subnet.Trim();
    }

    public string Id { get; }

    public string Name { get; }

    public string Driver { get; }

    public string? Subnet { get; }
}
