using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Répartition de l'utilisation CPU sur les modes rapportés par <c>/proc/stat</c>.
///
/// Les modes sont conservés séparément, et non agrégés en un seul pourcentage : c'est
/// exactement la décomposition que les maquettes affichent en aires empilées
/// (user / system / iowait / nice / steal), et un serveur bloqué en <c>iowait</c> ne se
/// diagnostique pas comme un serveur saturé en <c>user</c>.
/// </summary>
public sealed record CpuUsage
{
    public CpuUsage(
        Percentage user,
        Percentage system,
        Percentage ioWait,
        Percentage nice,
        Percentage steal,
        int coreCount)
    {
        User = user;
        System = system;
        IoWait = ioWait;
        Nice = nice;
        Steal = steal;
        CoreCount = Guard.InRange(coreCount, 1, 4096);
    }

    public Percentage User { get; }

    public Percentage System { get; }

    public Percentage IoWait { get; }

    public Percentage Nice { get; }

    public Percentage Steal { get; }

    public int CoreCount { get; }

    /// <summary>
    /// Utilisation totale, c'est-à-dire tout ce qui n'est pas du temps d'inactivité.
    /// Bornée : la somme des modes peut dépasser 100 % de quelques dixièmes par arrondi.
    /// </summary>
    public Percentage Total => Percentage.Clamp(
        User.Value + System.Value + IoWait.Value + Nice.Value + Steal.Value);

    public Percentage Idle => Percentage.Clamp(Percentage.Maximum - Total.Value);
}
