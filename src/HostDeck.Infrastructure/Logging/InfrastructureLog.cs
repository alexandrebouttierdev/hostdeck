using System;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Messages de journalisation de l'Infrastructure, générés à la compilation.
///
/// Même raison que dans la couche Application : ces messages sont émis dans la boucle de
/// collecte, et les surcharges à paramètres variables allouent et évaluent leurs arguments
/// même à niveau désactivé (§37, §39).
///
/// Aucun de ces messages ne prend de secret en paramètre (§51, T2).
/// </summary>
internal static partial class InfrastructureLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Local database ready at {DataSource}")]
    public static partial void DatabaseReady(ILogger logger, string dataSource);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Persisted {SampleCount} metric samples in {ElapsedMs} ms")]
    public static partial void MetricBatchPersisted(ILogger logger, int sampleCount, long elapsedMs);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Pruned {RowCount} rows older than {Cutoff}")]
    public static partial void RowsPruned(ILogger logger, int rowCount, DateTimeOffset cutoff);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "History query for server {ServerId} aggregated {Series} series into {BucketCount} buckets of {BucketMs} ms")]
    public static partial void HistoryAggregated(
        ILogger logger,
        Guid serverId,
        int series,
        int bucketCount,
        long bucketMs);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Host key for {Host}:{Port} was approved, replacing a previously approved fingerprint")]
    public static partial void HostKeyReplaced(ILogger logger, string host, int port);
}
