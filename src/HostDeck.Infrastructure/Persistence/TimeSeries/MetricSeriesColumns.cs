using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using HostDeck.Application.Dtos.Monitoring;

namespace HostDeck.Infrastructure.Persistence.TimeSeries;

/// <summary>
/// Correspondance entre une série traçable et l'expression SQL qui la produit.
///
/// <para>
/// Ce dictionnaire est la seule source d'expressions SQL pour les séries, et il n'est indexé
/// que par l'enum <see cref="MetricSeriesKind"/>. Aucune chaîne fournie par un appelant
/// n'atteint jamais une requête : les noms de colonnes ne pouvant pas être paramétrés en SQL,
/// c'est cette table fermée qui garantit qu'aucune injection n'est possible (§51, T5).
/// </para>
/// </summary>
internal static class MetricSeriesColumns
{
    /// <summary>
    /// Expressions issues de <c>metric_samples</c>, la table qui porte une ligne par cycle.
    /// </summary>
    private static readonly FrozenDictionary<MetricSeriesKind, string> SampleExpressions =
        new Dictionary<MetricSeriesKind, string>
        {
            [MetricSeriesKind.CpuUser] = "CpuUser",
            [MetricSeriesKind.CpuSystem] = "CpuSystem",
            [MetricSeriesKind.CpuIoWait] = "CpuIoWait",
            [MetricSeriesKind.CpuNice] = "CpuNice",
            [MetricSeriesKind.CpuSteal] = "CpuSteal",
            [MetricSeriesKind.CpuTotal] = "(CpuUser + CpuSystem + CpuIoWait + CpuNice + CpuSteal)",

            // Les octets sont convertis en pourcentage côté SQL : les graphiques et les seuils
            // raisonnent en proportion, et une division par zéro sur une machine sans swap
            // doit donner 0 plutôt que NULL.
            [MetricSeriesKind.MemoryUsed] =
                "(CASE WHEN MemoryTotalBytes > 0 THEN MemoryUsedBytes * 100.0 / MemoryTotalBytes ELSE 0 END)",
            [MetricSeriesKind.MemoryCached] =
                "(CASE WHEN MemoryTotalBytes > 0 THEN MemoryCachedBytes * 100.0 / MemoryTotalBytes ELSE 0 END)",
            [MetricSeriesKind.MemoryBuffers] =
                "(CASE WHEN MemoryTotalBytes > 0 THEN MemoryBuffersBytes * 100.0 / MemoryTotalBytes ELSE 0 END)",
            [MetricSeriesKind.MemoryFree] =
                "(CASE WHEN MemoryTotalBytes > 0 THEN (MemoryTotalBytes - MemoryUsedBytes) * 100.0 / MemoryTotalBytes ELSE 0 END)",
            [MetricSeriesKind.SwapUsed] =
                "(CASE WHEN SwapTotalBytes > 0 THEN SwapUsedBytes * 100.0 / SwapTotalBytes ELSE 0 END)",

            [MetricSeriesKind.LoadOne] = "LoadOne",
            [MetricSeriesKind.LoadFive] = "LoadFive",
            [MetricSeriesKind.LoadFifteen] = "LoadFifteen",
        }.ToFrozenDictionary();

    /// <summary>
    /// Séries qui ne viennent pas de <c>metric_samples</c> et exigent leur propre requête.
    /// </summary>
    public static bool IsDiskSeries(MetricSeriesKind kind) => kind == MetricSeriesKind.DiskUsed;

    public static bool IsNetworkSeries(MetricSeriesKind kind) =>
        kind is MetricSeriesKind.NetworkReceived or MetricSeriesKind.NetworkTransmitted;

    /// <summary>
    /// Renvoie l'expression SQL d'une série de <c>metric_samples</c>.
    /// </summary>
    public static bool TryGetSampleExpression(MetricSeriesKind kind, out string expression) =>
        SampleExpressions.TryGetValue(kind, out expression!);

    /// <summary>
    /// Colonne agrégée pour les séries réseau, dont les compteurs sont cumulés.
    /// </summary>
    public static string NetworkColumn(MetricSeriesKind kind) => kind switch
    {
        MetricSeriesKind.NetworkReceived => "ReceivedBytes",
        MetricSeriesKind.NetworkTransmitted => "TransmittedBytes",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "série réseau inconnue"),
    };
}
