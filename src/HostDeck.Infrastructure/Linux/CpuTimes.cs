using System;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Instantané des compteurs CPU agrégés de <c>/proc/stat</c>.
///
/// <para>
/// Linux ne fournit que des compteurs cumulés depuis le démarrage. Une utilisation en
/// pourcentage n'a donc de sens que comme différence entre deux relevés, ce qui explique
/// que <see cref="UsageSince"/> prenne le relevé précédent et que la collecte conserve un
/// état d'un cycle à l'autre.
/// </para>
///
/// <para>
/// Les valeurs sont en « ticks » (généralement 100 par seconde). <c>guest</c> et
/// <c>guest_nice</c> sont déjà comptés dans <c>user</c> et <c>nice</c> par le noyau :
/// ils ne sont donc pas additionnés une seconde fois.
/// </para>
/// </summary>
internal readonly record struct CpuTimes
{
    public CpuTimes(
        long user,
        long nice,
        long system,
        long idle,
        long ioWait,
        long irq,
        long softIrq,
        long steal,
        int coreCount)
    {
        User = Math.Max(0, user);
        Nice = Math.Max(0, nice);
        System = Math.Max(0, system);
        Idle = Math.Max(0, idle);
        IoWait = Math.Max(0, ioWait);
        Irq = Math.Max(0, irq);
        SoftIrq = Math.Max(0, softIrq);
        Steal = Math.Max(0, steal);
        CoreCount = Math.Max(0, coreCount);
    }

    public long User { get; }

    public long Nice { get; }

    public long System { get; }

    public long Idle { get; }

    public long IoWait { get; }

    public long Irq { get; }

    public long SoftIrq { get; }

    public long Steal { get; }

    /// <summary>Nombre de cœurs déduit des lignes <c>cpuN</c> de <c>/proc/stat</c>.</summary>
    public int CoreCount { get; }

    /// <summary>
    /// Total de ticks couvrant tous les modes. <c>irq</c> et <c>softirq</c> y figurent pour
    /// que le dénominateur soit le temps réellement écoulé, mais ne sont pas exposés comme
    /// modes distincts de l'interface : ils font partie du « reste » face à l'inactivité.
    /// </summary>
    public long Total =>
        checked(User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal);

    /// <summary>
    /// Calcule l'utilisation CPU entre ce relevé et un relevé antérieur.
    ///
    /// <para>
    /// Un compteur qui recule (redémarrage, remise à zéro, dépassement) donne zéro plutôt
    /// qu'un pourcentage négatif ou aberrant : le delta n'est jamais négatif.
    /// </para>
    ///
    /// <para>
    /// Les modes demandés par les maquettes — user, system, iowait, nice, steal — sont
    /// exprimés en part du temps total écoulé. Le reste (idle + irq + softirq) n'apparaît
    /// pas comme mode : c'est l'inactivité.
    /// </para>
    /// </summary>
    public CpuUsage UsageSince(CpuTimes previous)
    {
        var totalDelta = Math.Max(0d, Total - previous.Total);

        if (totalDelta <= 0d)
        {
            return new CpuUsage(
                Percentage.Zero,
                Percentage.Zero,
                Percentage.Zero,
                Percentage.Zero,
                Percentage.Zero,
                EffectiveCoreCount);
        }

        static double Share(double delta, double total) => delta * 100d / total;

        return new CpuUsage(
            Percentage.Clamp(Share(Math.Max(0, User - previous.User), totalDelta)),
            Percentage.Clamp(Share(Math.Max(0, System - previous.System), totalDelta)),
            Percentage.Clamp(Share(Math.Max(0, IoWait - previous.IoWait), totalDelta)),
            Percentage.Clamp(Share(Math.Max(0, Nice - previous.Nice), totalDelta)),
            Percentage.Clamp(Share(Math.Max(0, Steal - previous.Steal), totalDelta)),
            EffectiveCoreCount);
    }

    /// <summary>
    /// Un hôte conteneurisé peut n'exposer que la ligne agrégée <c>cpu</c>, sans ligne
    /// <c>cpuN</c>. Le domaine exige au moins un cœur : on retombe alors sur 1.
    /// </summary>
    public int EffectiveCoreCount => CoreCount > 0 ? CoreCount : 1;
}
