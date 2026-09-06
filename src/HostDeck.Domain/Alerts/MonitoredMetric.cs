namespace HostDeck.Domain.Alerts;

/// <summary>
/// Grandeur sur laquelle une règle d'alerte peut porter (§21).
///
/// Un enum fermé, et non une chaîne libre : une règle visant une métrique inexistante ne
/// doit pas pouvoir être créée, et le moteur d'évaluation doit être exhaustif par
/// construction.
/// </summary>
public enum MonitoredMetric
{
    /// <summary>L'hôte ne répond pas.</summary>
    ServerUnavailable = 0,

    /// <summary>Le bastion ne répond pas, donc la cible est inatteignable.</summary>
    GatewayUnavailable = 1,

    /// <summary>Utilisation CPU totale, en pourcentage.</summary>
    CpuUsage = 2,

    /// <summary>Mémoire utilisée, en pourcentage.</summary>
    MemoryUsage = 3,

    /// <summary>Occupation du point de montage le plus rempli, en pourcentage.</summary>
    DiskUsage = 4,

    /// <summary>Charge système sur 1 minute.</summary>
    LoadAverage = 5,

    /// <summary>Latence de la connexion SSH, en millisecondes.</summary>
    SshLatency = 6,

    /// <summary>Utilisation du swap, en pourcentage.</summary>
    SwapUsage = 7,

    /// <summary>Un conteneur attendu est arrêté.</summary>
    DockerContainerStopped = 8,

    /// <summary>Un conteneur est rapporté comme non sain par son health check.</summary>
    DockerContainerUnhealthy = 9,

    /// <summary>Nombre de redémarrages d'un conteneur.</summary>
    DockerRestartCount = 10,
}

/// <summary>
/// Indique si une métrique s'exprime en pourcentage, ce qui conditionne l'unité affichée
/// et la validation du seuil.
/// </summary>
public static class MonitoredMetricExtensions
{
    public static bool IsPercentage(this MonitoredMetric metric) => metric switch
    {
        MonitoredMetric.CpuUsage
            or MonitoredMetric.MemoryUsage
            or MonitoredMetric.DiskUsage
            or MonitoredMetric.SwapUsage => true,
        _ => false,
    };

    /// <summary>
    /// Vrai pour les métriques qui expriment une disponibilité binaire : elles n'ont pas de
    /// seuil numérique, la condition est le fait lui-même.
    /// </summary>
    public static bool IsStateBased(this MonitoredMetric metric) => metric switch
    {
        MonitoredMetric.ServerUnavailable
            or MonitoredMetric.GatewayUnavailable
            or MonitoredMetric.DockerContainerStopped
            or MonitoredMetric.DockerContainerUnhealthy => true,
        _ => false,
    };
}
