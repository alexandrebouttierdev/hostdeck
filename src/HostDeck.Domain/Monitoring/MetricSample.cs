using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Résultat d'un cycle de collecte pour un serveur, à un instant donné.
///
/// C'est un instantané immuable : une fois écrit, il n'est jamais modifié, seulement lu,
/// agrégé ou purgé. Cette immuabilité est ce qui permet de le partager entre le collecteur,
/// la persistance et le thread de rendu sans synchronisation (§49).
/// </summary>
public sealed record MetricSample
{
    public MetricSample(
        ServerId serverId,
        DateTimeOffset observedAt,
        CpuUsage cpu,
        MemoryUsage memory,
        SwapUsage swap,
        LoadAverage load,
        Uptime uptime,
        IEnumerable<DiskUsage>? disks = null,
        IEnumerable<NetworkUsage>? interfaces = null)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(swap);

        ServerId = serverId;
        // Toujours stocké en UTC : la conversion en heure locale appartient à la
        // Presentation, et un historique mélangeant des fuseaux serait ininterprétable (§61).
        ObservedAt = Guard.RequiredInstant(observedAt).ToUniversalTime();
        Cpu = cpu;
        Memory = memory;
        Swap = swap;
        Load = load;
        Uptime = uptime;
        Disks = disks is null ? [] : [.. disks];
        Interfaces = interfaces is null ? [] : [.. interfaces];
    }

    public ServerId ServerId { get; }

    /// <summary>Instant de l'observation, en UTC.</summary>
    public DateTimeOffset ObservedAt { get; }

    public CpuUsage Cpu { get; }

    public MemoryUsage Memory { get; }

    public SwapUsage Swap { get; }

    public LoadAverage Load { get; }

    public Uptime Uptime { get; }

    public ImmutableArray<DiskUsage> Disks { get; }

    public ImmutableArray<NetworkUsage> Interfaces { get; }

    /// <summary>
    /// Partition la plus remplie, celle qui détermine si un seuil disque est franchi.
    /// </summary>
    public DiskUsage? BusiestDisk
    {
        get
        {
            DiskUsage? busiest = null;
            foreach (var disk in Disks)
            {
                if (busiest is null || disk.UsedRatio.Value > busiest.UsedRatio.Value)
                {
                    busiest = disk;
                }
            }

            return busiest;
        }
    }

    /// <summary>
    /// Détecte un redémarrage entre deux relevés : la durée de fonctionnement a reculé.
    /// Un redémarrage invalide les compteurs cumulés, donc tout calcul de débit basé dessus.
    /// </summary>
    public bool IndicatesRebootSince(MetricSample previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return Uptime.Value < previous.Uptime.Value;
    }
}
