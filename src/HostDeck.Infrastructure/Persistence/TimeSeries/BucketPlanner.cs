using System;

namespace HostDeck.Infrastructure.Persistence.TimeSeries;

/// <summary>
/// Choisit la largeur des buckets d'agrégation d'un historique.
///
/// <para>
/// Une plage de 30 jours collectée toutes les 60 secondes représente 43 200 points pour une
/// série. Un graphique large de 900 pixels ne peut pas en montrer plus de 900 : charger le
/// reste coûterait la mémoire et le temps d'analyse de 42 000 points jetés à l'affichage (§19).
/// </para>
///
/// <para>
/// La largeur est arrondie à une valeur « ronde » plutôt qu'au strict quotient. Un bucket de
/// 47 secondes ferait glisser les frontières d'un rafraîchissement à l'autre, et le tracé
/// scintillerait alors que les données n'ont pas changé.
/// </para>
/// </summary>
internal static class BucketPlanner
{
    /// <summary>
    /// Largeurs candidates, de la seconde au jour. Ce sont les graduations qu'un opérateur
    /// lit naturellement sur un axe de temps.
    /// </summary>
    private static readonly long[] CandidateMilliseconds =
    [
        1_000,           // 1 s
        5_000,           // 5 s
        10_000,          // 10 s
        15_000,          // 15 s
        30_000,          // 30 s
        60_000,          // 1 min
        120_000,         // 2 min
        300_000,         // 5 min
        600_000,         // 10 min
        900_000,         // 15 min
        1_800_000,       // 30 min
        3_600_000,       // 1 h
        7_200_000,       // 2 h
        21_600_000,      // 6 h
        43_200_000,      // 12 h
        86_400_000,      // 1 j
    ];

    public const int MinimumPoints = 1;
    public const int MaximumPoints = 5_000;

    /// <summary>
    /// Renvoie la plus petite largeur ronde qui garde le nombre de points sous la limite.
    /// </summary>
    public static TimeSpan ChooseBucket(DateTimeOffset from, DateTimeOffset to, int maxPoints)
    {
        var points = Math.Clamp(maxPoints, MinimumPoints, MaximumPoints);
        var spanMilliseconds = (long)(to - from).TotalMilliseconds;

        if (spanMilliseconds <= 0)
        {
            return TimeSpan.FromMilliseconds(CandidateMilliseconds[0]);
        }

        var idealMilliseconds = spanMilliseconds / points;

        foreach (var candidate in CandidateMilliseconds)
        {
            if (candidate >= idealMilliseconds)
            {
                return TimeSpan.FromMilliseconds(candidate);
            }
        }

        // Plage si longue qu'un bucket d'un jour dépasserait encore la limite : on élargit
        // par multiples de jours plutôt que de renvoyer un nombre de points ingérable.
        var days = (long)Math.Ceiling(idealMilliseconds / 86_400_000d);
        return TimeSpan.FromMilliseconds(days * 86_400_000L);
    }
}
