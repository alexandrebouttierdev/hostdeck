using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Docker;

/// <summary>Identifiant d'un conteneur, tel que le moteur Docker le rapporte.</summary>
public readonly record struct DockerContainerId
{
    public const int MaxLength = 64;

    public DockerContainerId(string value)
    {
        Value = Guard.RequiredText(value, MaxLength);
    }

    public string Value { get; }

    /// <summary>Forme abrégée à 12 caractères, celle qu'affichent les outils Docker.</summary>
    public string ShortId => Value.Length <= 12 ? Value : Value[..12];

    public override string ToString() => ShortId;
}

/// <summary>
/// État d'exécution d'un conteneur, aligné sur les états du moteur Docker.
/// </summary>
public enum DockerContainerStatus
{
    Created = 0,
    Running = 1,
    Paused = 2,
    Restarting = 3,
    Removing = 4,
    Exited = 5,
    Dead = 6,
    Unknown = 7,
}

/// <summary>
/// Résultat du health check d'un conteneur. Distinct de l'état d'exécution : un conteneur
/// peut tourner tout en étant non sain, ce qui est précisément le cas à alerter.
/// </summary>
public enum DockerHealthStatus
{
    /// <summary>Aucun health check défini par l'image.</summary>
    None = 0,
    Starting = 1,
    Healthy = 2,
    Unhealthy = 3,
}

/// <summary>
/// Conteneur observé sur un hôte supervisé.
/// </summary>
public sealed record DockerContainer
{
    public const int MaxNameLength = 255;
    public const int MaxImageLength = 512;

    public DockerContainer(
        DockerContainerId id,
        ServerId serverId,
        string name,
        string image,
        DockerContainerStatus status,
        DockerHealthStatus health,
        int restartCount,
        DateTimeOffset? startedAt = null,
        IEnumerable<string>? ports = null)
    {
        Id = id;
        ServerId = serverId;
        Name = Guard.RequiredText(name, MaxNameLength);
        Image = Guard.RequiredText(image, MaxImageLength);
        Status = status;
        Health = health;
        RestartCount = Guard.NotNegative(restartCount);
        StartedAt = startedAt?.ToUniversalTime();
        Ports = ports is null ? [] : [.. ports];
    }

    public DockerContainerId Id { get; }

    public ServerId ServerId { get; }

    public string Name { get; }

    public string Image { get; }

    public DockerContainerStatus Status { get; }

    public DockerHealthStatus Health { get; }

    /// <summary>
    /// Nombre de redémarrages. Un compteur qui grimpe signale une boucle de crash, souvent
    /// avant que le conteneur ne soit durablement arrêté.
    /// </summary>
    public int RestartCount { get; }

    public DateTimeOffset? StartedAt { get; }

    public ImmutableArray<string> Ports { get; }

    public bool IsRunning => Status == DockerContainerStatus.Running;

    public bool IsUnhealthy => Health == DockerHealthStatus.Unhealthy;

    public TimeSpan? UptimeAt(DateTimeOffset now) =>
        IsRunning && StartedAt is { } started ? now.ToUniversalTime() - started : null;
}
