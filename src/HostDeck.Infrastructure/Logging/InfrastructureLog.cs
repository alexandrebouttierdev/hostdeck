using System;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure;

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

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "SSH connected to {Host}:{Port} in {ElapsedMs} ms (jump={ViaJump})")]
    public static partial void SshConnected(ILogger logger, string host, int port, long elapsedMs, bool viaJump);

    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Credential written for key {CredentialKey}")]
    public static partial void CredentialWritten(ILogger logger, string credentialKey);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Credential deleted for key {CredentialKey}")]
    public static partial void CredentialDeleted(ILogger logger, string credentialKey);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Warning,
        Message = "OS credential store is unavailable")]
    public static partial void CredentialStoreUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Information,
        Message = "Fleet monitoring coordinator started")]
    public static partial void FleetCoordinatorStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2301,
        Level = LogLevel.Information,
        Message = "Fleet monitoring coordinator stopped")]
    public static partial void FleetCoordinatorStopped(ILogger logger);

    [LoggerMessage(
        EventId = 2302,
        Level = LogLevel.Information,
        Message = "Fleet cycle completed: {Succeeded} succeeded, {Failed} failed in {ElapsedMs} ms")]
    public static partial void FleetCycleCompleted(ILogger logger, int succeeded, int failed, long elapsedMs);

    [LoggerMessage(
        EventId = 2303,
        Level = LogLevel.Warning,
        Message = "Collect attempt {Attempt}/{MaxAttempts} failed for server {ServerId}")]
    public static partial void FleetCollectAttemptFailed(
        ILogger logger,
        Exception exception,
        Guid serverId,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 2304,
        Level = LogLevel.Error,
        Message = "Failed to persist status for server {ServerId}")]
    public static partial void FleetStatusPersistFailed(ILogger logger, Exception exception, Guid serverId);

    [LoggerMessage(
        EventId = 2305,
        Level = LogLevel.Error,
        Message = "Failed to evaluate incidents for server {ServerId}")]
    public static partial void FleetIncidentEvaluateFailed(ILogger logger, Exception exception, Guid serverId);
}
