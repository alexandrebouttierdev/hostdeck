using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Identité du système d'exploitation d'un hôte, issue de <c>/etc/os-release</c> et
/// <c>uname</c>.
///
/// Collectée bien plus rarement que les métriques : elle ne change qu'à une mise à jour
/// ou un redémarrage.
/// </summary>
public sealed record SystemIdentity
{
    public const int MaxLength = 256;

    public SystemIdentity(string hostname, string operatingSystem, string kernelVersion)
    {
        Hostname = Guard.RequiredText(hostname, MaxLength);
        OperatingSystem = Guard.RequiredText(operatingSystem, MaxLength);
        KernelVersion = Guard.RequiredText(kernelVersion, MaxLength);
    }

    /// <summary>Nom d'hôte rapporté par la machine, qui peut différer du nom configuré.</summary>
    public string Hostname { get; }

    /// <summary>Distribution et version, par exemple « Ubuntu 22.04.4 LTS ».</summary>
    public string OperatingSystem { get; }

    /// <summary>Version du noyau, par exemple « 5.15.0-105-generic ».</summary>
    public string KernelVersion { get; }
}

/// <summary>
/// Durée de fonctionnement depuis le dernier démarrage.
/// </summary>
public readonly record struct Uptime
{
    public Uptime(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new DomainValidationException(nameof(value), "une durée de fonctionnement est positive");
        }

        Value = value;
    }

    public TimeSpan Value { get; }

    public static Uptime FromSeconds(double seconds) =>
        new(TimeSpan.FromSeconds(Guard.NotNegative(seconds)));

    /// <summary>
    /// Instant du démarrage, déduit d'un instant d'observation. Utile pour détecter un
    /// redémarrage entre deux collectes.
    /// </summary>
    public DateTimeOffset BootedAt(DateTimeOffset observedAt) => observedAt - Value;

    public override string ToString() => Value.ToString();
}
