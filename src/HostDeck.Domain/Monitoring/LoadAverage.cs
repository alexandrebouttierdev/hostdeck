using System.Globalization;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Charge système moyenne sur 1, 5 et 15 minutes, telle que rapportée par
/// <c>/proc/loadavg</c>.
///
/// La charge n'est pas un pourcentage : sa lecture dépend du nombre de cœurs, d'où
/// <see cref="PerCore"/>, qui est la forme réellement comparable entre machines.
/// </summary>
public readonly record struct LoadAverage
{
    public LoadAverage(double oneMinute, double fiveMinutes, double fifteenMinutes)
    {
        OneMinute = Guard.NotNegative(oneMinute);
        FiveMinutes = Guard.NotNegative(fiveMinutes);
        FifteenMinutes = Guard.NotNegative(fifteenMinutes);
    }

    public double OneMinute { get; }

    public double FiveMinutes { get; }

    public double FifteenMinutes { get; }

    /// <summary>
    /// Charge sur 1 minute rapportée au nombre de cœurs. Une valeur de 1,0 signifie que la
    /// machine est exactement saturée, quel que soit son dimensionnement.
    /// </summary>
    public double PerCore(int coreCount) =>
        coreCount <= 0 ? OneMinute : OneMinute / coreCount;

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{OneMinute:0.00} {FiveMinutes:0.00} {FifteenMinutes:0.00}");
}
