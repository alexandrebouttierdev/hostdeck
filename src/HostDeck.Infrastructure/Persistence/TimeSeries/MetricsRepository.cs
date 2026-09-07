using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Persistence.TimeSeries;

/// <summary>
/// Dépôt des séries temporelles, en SQL paramétré brut.
///
/// <para>
/// C'est le chemin chaud du produit : une flotte de cent hôtes collectée chaque minute produit
/// 144 000 lignes par jour, et un graphique de 30 jours doit les agréger sans les matérialiser.
/// Le suivi de changements d'EF Core, sa matérialisation d'entités et son expression LINQ des
/// agrégats sont inadaptés à ce volume ; le schéma reste néanmoins décrit par EF, qui crée les
/// tables et les index (§18, §19).
/// </para>
///
/// <para>
/// Toutes les valeurs passent par des paramètres. Les seuls fragments SQL interpolés sont des
/// expressions de colonnes issues d'une table fermée indexée par enum, jamais d'une chaîne
/// fournie par un appelant (§51, T5).
/// </para>
/// </summary>
internal sealed class MetricsRepository : IMetricsRepository
{
    /// <summary>
    /// Instructions de purge, constantes et complètes. Écrites ici une fois pour toutes afin
    /// qu'aucun nom de table ne soit composé à l'exécution.
    /// </summary>
    private static readonly string[] PruneStatements =
    [
        "DELETE FROM metric_samples WHERE ObservedAt < $cutoff",
        "DELETE FROM disk_metric_samples WHERE ObservedAt < $cutoff",
        "DELETE FROM network_metric_samples WHERE ObservedAt < $cutoff",
    ];

    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly ILogger<MetricsRepository> _logger;

    public MetricsRepository(
        IDbContextFactory<HostDeckDbContext> contextFactory,
        ILogger<MetricsRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task AddBatchAsync(
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        // Une seule transaction pour tout le lot. SQLite valide chaque transaction sur le
        // disque : une transaction par ligne rendrait un cycle de collecte visiblement lent.
        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await InsertSamplesAsync(connection, transaction, samples, cancellationToken).ConfigureAwait(false);
            await InsertDisksAsync(connection, transaction, samples, cancellationToken).ConfigureAwait(false);
            await InsertInterfacesAsync(connection, transaction, samples, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw new RepositoryException("persist metric batch", exception);
        }

        InfrastructureLog.MetricBatchPersisted(_logger, samples.Count, stopwatch.ElapsedMilliseconds);
    }

    private static async Task InsertSamplesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO metric_samples (
                ServerId, ObservedAt,
                CpuUser, CpuSystem, CpuIoWait, CpuNice, CpuSteal, CoreCount,
                MemoryTotalBytes, MemoryUsedBytes, MemoryCachedBytes, MemoryBuffersBytes,
                SwapTotalBytes, SwapUsedBytes,
                LoadOne, LoadFive, LoadFifteen, UptimeSeconds)
            VALUES (
                $serverId, $observedAt,
                $cpuUser, $cpuSystem, $cpuIoWait, $cpuNice, $cpuSteal, $coreCount,
                $memTotal, $memUsed, $memCached, $memBuffers,
                $swapTotal, $swapUsed,
                $load1, $load5, $load15, $uptime)
            """;

        // Les paramètres sont créés une fois et seules leurs valeurs changent : préparer la
        // commande une seule fois évite de réanalyser le SQL à chaque ligne du lot.
        var parameters = command.Parameters;
        parameters.Add("$serverId", SqliteType.Text);
        parameters.Add("$observedAt", SqliteType.Integer);
        parameters.Add("$cpuUser", SqliteType.Real);
        parameters.Add("$cpuSystem", SqliteType.Real);
        parameters.Add("$cpuIoWait", SqliteType.Real);
        parameters.Add("$cpuNice", SqliteType.Real);
        parameters.Add("$cpuSteal", SqliteType.Real);
        parameters.Add("$coreCount", SqliteType.Integer);
        parameters.Add("$memTotal", SqliteType.Integer);
        parameters.Add("$memUsed", SqliteType.Integer);
        parameters.Add("$memCached", SqliteType.Integer);
        parameters.Add("$memBuffers", SqliteType.Integer);
        parameters.Add("$swapTotal", SqliteType.Integer);
        parameters.Add("$swapUsed", SqliteType.Integer);
        parameters.Add("$load1", SqliteType.Real);
        parameters.Add("$load5", SqliteType.Real);
        parameters.Add("$load15", SqliteType.Real);
        parameters.Add("$uptime", SqliteType.Real);

        foreach (var sample in samples)
        {
            parameters["$serverId"].Value = ToDatabaseId(sample.ServerId);
            parameters["$observedAt"].Value = sample.ObservedAt.ToUnixTimeMilliseconds();
            parameters["$cpuUser"].Value = sample.Cpu.User.Value;
            parameters["$cpuSystem"].Value = sample.Cpu.System.Value;
            parameters["$cpuIoWait"].Value = sample.Cpu.IoWait.Value;
            parameters["$cpuNice"].Value = sample.Cpu.Nice.Value;
            parameters["$cpuSteal"].Value = sample.Cpu.Steal.Value;
            parameters["$coreCount"].Value = sample.Cpu.CoreCount;
            parameters["$memTotal"].Value = sample.Memory.Total.Bytes;
            parameters["$memUsed"].Value = sample.Memory.Used.Bytes;
            parameters["$memCached"].Value = sample.Memory.Cached.Bytes;
            parameters["$memBuffers"].Value = sample.Memory.Buffers.Bytes;
            parameters["$swapTotal"].Value = sample.Swap.Total.Bytes;
            parameters["$swapUsed"].Value = sample.Swap.Used.Bytes;
            parameters["$load1"].Value = sample.Load.OneMinute;
            parameters["$load5"].Value = sample.Load.FiveMinutes;
            parameters["$load15"].Value = sample.Load.FifteenMinutes;
            parameters["$uptime"].Value = sample.Uptime.Value.TotalSeconds;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InsertDisksAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO disk_metric_samples (ServerId, ObservedAt, MountPoint, FileSystem, TotalBytes, UsedBytes)
            VALUES ($serverId, $observedAt, $mountPoint, $fileSystem, $total, $used)
            """;

        var parameters = command.Parameters;
        parameters.Add("$serverId", SqliteType.Text);
        parameters.Add("$observedAt", SqliteType.Integer);
        parameters.Add("$mountPoint", SqliteType.Text);
        parameters.Add("$fileSystem", SqliteType.Text);
        parameters.Add("$total", SqliteType.Integer);
        parameters.Add("$used", SqliteType.Integer);

        foreach (var sample in samples)
        {
            foreach (var disk in sample.Disks)
            {
                parameters["$serverId"].Value = ToDatabaseId(sample.ServerId);
                parameters["$observedAt"].Value = sample.ObservedAt.ToUnixTimeMilliseconds();
                parameters["$mountPoint"].Value = disk.MountPoint;
                parameters["$fileSystem"].Value = (object?)disk.FileSystem ?? DBNull.Value;
                parameters["$total"].Value = disk.Total.Bytes;
                parameters["$used"].Value = disk.Used.Bytes;

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task InsertInterfacesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<MetricSample> samples,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO network_metric_samples (ServerId, ObservedAt, InterfaceName, ReceivedBytes, TransmittedBytes)
            VALUES ($serverId, $observedAt, $interface, $received, $transmitted)
            """;

        var parameters = command.Parameters;
        parameters.Add("$serverId", SqliteType.Text);
        parameters.Add("$observedAt", SqliteType.Integer);
        parameters.Add("$interface", SqliteType.Text);
        parameters.Add("$received", SqliteType.Integer);
        parameters.Add("$transmitted", SqliteType.Integer);

        foreach (var sample in samples)
        {
            foreach (var netInterface in sample.Interfaces)
            {
                parameters["$serverId"].Value = ToDatabaseId(sample.ServerId);
                parameters["$observedAt"].Value = sample.ObservedAt.ToUnixTimeMilliseconds();
                parameters["$interface"].Value = netInterface.InterfaceName;
                parameters["$received"].Value = netInterface.Received.Bytes;
                parameters["$transmitted"].Value = netInterface.Transmitted.Bytes;

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<LatestMetricDto?> GetLatestAsync(
        ServerId serverId,
        CancellationToken cancellationToken = default)
    {
        var results = await ReadLatestAsync(serverId, cancellationToken).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : null;
    }

    public Task<IReadOnlyList<LatestMetricDto>> GetLatestForAllAsync(
        CancellationToken cancellationToken = default) =>
        ReadLatestAsync(serverId: null, cancellationToken);

    /// <summary>
    /// Lit la dernière valeur connue de chaque serveur.
    ///
    /// La sous-requête sélectionne d'abord le dernier <c>ObservedAt</c> par serveur, ce que
    /// l'index (ServerId, ObservedAt) résout sans parcourir la table. Une jointure naïve sur
    /// le maximum lirait toute l'historique à chaque affichage de l'inventaire.
    /// </summary>
    private async Task<IReadOnlyList<LatestMetricDto>> ReadLatestAsync(
        ServerId? serverId,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH latest AS (
                SELECT ServerId, MAX(ObservedAt) AS ObservedAt
                FROM metric_samples
                WHERE ($serverId IS NULL OR ServerId = $serverId)
                GROUP BY ServerId
            )
            SELECT
                s.ServerId,
                s.ObservedAt,
                (s.CpuUser + s.CpuSystem + s.CpuIoWait + s.CpuNice + s.CpuSteal) AS CpuPercent,
                CASE WHEN s.MemoryTotalBytes > 0
                     THEN s.MemoryUsedBytes * 100.0 / s.MemoryTotalBytes ELSE 0 END AS MemoryPercent,
                COALESCE((
                    SELECT MAX(CASE WHEN d.TotalBytes > 0
                                    THEN d.UsedBytes * 100.0 / d.TotalBytes ELSE 0 END)
                    FROM disk_metric_samples d
                    WHERE d.ServerId = s.ServerId AND d.ObservedAt = s.ObservedAt), 0) AS DiskPercent,
                s.LoadOne,
                s.UptimeSeconds
            FROM metric_samples s
            JOIN latest ON latest.ServerId = s.ServerId AND latest.ObservedAt = s.ObservedAt
            """;

        command.Parameters.Add("$serverId", SqliteType.Text).Value =
            serverId is { } id ? ToDatabaseId(id) : DBNull.Value;

        var results = new List<LatestMetricDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new LatestMetricDto
            {
                ServerId = Guid.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                ObservedAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)),
                CpuPercent = reader.GetDouble(2),
                MemoryPercent = reader.GetDouble(3),
                DiskPercent = reader.GetDouble(4),
                LoadOneMinute = reader.GetDouble(5),
                Uptime = TimeSpan.FromSeconds(reader.GetDouble(6)),
            });
        }

        return results;
    }

    public async Task<MetricHistoryDto> GetHistoryAsync(
        MetricHistoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bucket = BucketPlanner.ChooseBucket(request.From, request.To, request.MaxPoints);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        var series = new List<MetricSeriesDto>(request.Series.Count);
        var bucketCount = 0;

        foreach (var kind in request.Series)
        {
            var points = await ReadSeriesAsync(connection, request, kind, bucket, cancellationToken)
                .ConfigureAwait(false);

            bucketCount = Math.Max(bucketCount, points.Count);
            series.Add(new MetricSeriesDto { Kind = kind, Points = points });
        }

        InfrastructureLog.HistoryAggregated(
            _logger,
            request.ServerId,
            request.Series.Count,
            bucketCount,
            (long)bucket.TotalMilliseconds);

        return new MetricHistoryDto
        {
            ServerId = request.ServerId,
            From = request.From,
            To = request.To,
            BucketSize = bucket,
            Series = series,
        };
    }

    /// <summary>
    /// Agrège une série en buckets, en conservant min, moyenne et max.
    ///
    /// Les extrêmes sont conservés délibérément : après regroupement, ne garder que la
    /// moyenne effacerait les pics, c'est-à-dire précisément ce qu'on cherche dans un
    /// graphique de supervision (§19).
    /// </summary>
    private static async Task<IReadOnlyList<MetricPointDto>> ReadSeriesAsync(
        SqliteConnection connection,
        MetricHistoryRequestDto request,
        MetricSeriesKind kind,
        TimeSpan bucket,
        CancellationToken cancellationToken)
    {
        var bucketMilliseconds = (long)bucket.TotalMilliseconds;

        await using var command = connection.CreateCommand();

        // CA2100 ne peut pas prouver l'origine du SQL composé. Elle est pourtant close :
        // BuildSeriesSql n'interpole que des expressions issues de MetricSeriesColumns, une
        // table indexée par l'enum MetricSeriesKind. Aucune chaîne d'appelant n'y entre, et
        // toutes les valeurs restent des paramètres. Les noms de colonnes ne pouvant pas
        // être paramétrés en SQL, il n'existe pas d'alternative sans interpolation.
#pragma warning disable CA2100
        command.CommandText = BuildSeriesSql(kind);
#pragma warning restore CA2100

        command.Parameters.Add("$serverId", SqliteType.Text).Value =
            request.ServerId.ToString("D").ToUpperInvariant();
        command.Parameters.Add("$from", SqliteType.Integer).Value = request.From.ToUnixTimeMilliseconds();
        command.Parameters.Add("$to", SqliteType.Integer).Value = request.To.ToUnixTimeMilliseconds();
        command.Parameters.Add("$bucket", SqliteType.Integer).Value = bucketMilliseconds;

        var points = new List<MetricPointDto>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            points.Add(new MetricPointDto(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0) * bucketMilliseconds),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3)));
        }

        return points;
    }

    /// <summary>
    /// Construit la requête d'agrégation d'une série.
    ///
    /// L'expression de colonne provient exclusivement de <see cref="MetricSeriesColumns"/>,
    /// une table fermée indexée par enum : les noms de colonnes ne pouvant pas être des
    /// paramètres SQL, c'est ce qui garantit qu'aucune chaîne d'appelant n'entre ici.
    /// </summary>
    private static string BuildSeriesSql(MetricSeriesKind kind)
    {
        if (MetricSeriesColumns.IsDiskSeries(kind))
        {
            // Le disque agrège d'abord par relevé, en retenant la partition la plus remplie :
            // c'est elle qui déclenche l'incident, pas la moyenne des partitions.
            return """
                WITH busiest AS (
                    SELECT ObservedAt,
                           MAX(CASE WHEN TotalBytes > 0 THEN UsedBytes * 100.0 / TotalBytes ELSE 0 END) AS Value
                    FROM disk_metric_samples
                    WHERE ServerId = $serverId AND ObservedAt >= $from AND ObservedAt <= $to
                    GROUP BY ObservedAt
                )
                SELECT ObservedAt / $bucket AS Bucket, MIN(Value), AVG(Value), MAX(Value)
                FROM busiest
                GROUP BY Bucket
                ORDER BY Bucket
                """;
        }

        if (MetricSeriesColumns.IsNetworkSeries(kind))
        {
            // Les compteurs réseau sont cumulés : on somme d'abord les interfaces d'un même
            // relevé, puis on agrège les totaux par bucket.
            var column = MetricSeriesColumns.NetworkColumn(kind);
            return $"""
                WITH totals AS (
                    SELECT ObservedAt, SUM({column}) AS Value
                    FROM network_metric_samples
                    WHERE ServerId = $serverId AND ObservedAt >= $from AND ObservedAt <= $to
                    GROUP BY ObservedAt
                )
                SELECT ObservedAt / $bucket AS Bucket, MIN(Value), AVG(Value), MAX(Value)
                FROM totals
                GROUP BY Bucket
                ORDER BY Bucket
                """;
        }

        if (!MetricSeriesColumns.TryGetSampleExpression(kind, out var expression))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "série non prise en charge");
        }

        return $"""
            SELECT ObservedAt / $bucket AS Bucket,
                   MIN({expression}), AVG({expression}), MAX({expression})
            FROM metric_samples
            WHERE ServerId = $serverId AND ObservedAt >= $from AND ObservedAt <= $to
            GROUP BY Bucket
            ORDER BY Bucket
            """;
    }

    public async Task<IReadOnlyList<MetricStatisticsDto>> GetStatisticsAsync(
        ServerId serverId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<MetricSeriesKind> series,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        // Un bucket unique couvrant toute la plage : min, moyenne et max de la période
        // entière, ce que la vue « Données en direct » affiche à côté de chaque graphique.
        var request = new MetricHistoryRequestDto
        {
            ServerId = serverId.Value,
            From = from,
            To = to,
            Series = series,
            MaxPoints = 1,
        };

        var history = await GetHistoryAsync(request, cancellationToken).ConfigureAwait(false);

        return
        [
            .. history.Series.Select(entry =>
            {
                var point = entry.Points.Count > 0 ? entry.Points[^1] : default;

                return new MetricStatisticsDto
                {
                    Kind = entry.Kind,
                    Current = entry.Points.Count > 0 ? point.Average : null,
                    Minimum = point.Minimum,
                    Average = point.Average,
                    Maximum = point.Maximum,
                    SampleCount = entry.Points.Count,
                };
            })
        ];
    }

    /// <summary>
    /// Supprime les relevés antérieurs à la coupure, dans les trois tables de métriques.
    /// </summary>
    public async Task<int> PruneOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var removed = 0;

        try
        {
            // Les trois instructions sont écrites en toutes lettres plutôt que générées
            // dans une boucle : aucune interpolation, donc aucun doute possible sur
            // l'origine du SQL exécuté (§51, T5).
            foreach (var statement in PruneStatements)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                // CA2100 ne remonte pas jusqu'à l'origine de la chaîne. PruneStatements est
                // un tableau statique de littéraux, sans aucune valeur composée ; la date de
                // coupure passe par un paramètre lié.
#pragma warning disable CA2100
                command.CommandText = statement;
#pragma warning restore CA2100
                command.Parameters.Add("$cutoff", SqliteType.Integer).Value = cutoff.ToUnixTimeMilliseconds();

                removed += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw new RepositoryException("prune metrics", exception);
        }

        InfrastructureLog.RowsPruned(_logger, removed, cutoff);
        return removed;
    }

    /// <summary>
    /// Les identifiants sont écrits sous leur forme canonique à tirets, celle qu'EF Core
    /// utilise pour les colonnes Guid : les deux chemins d'accès doivent produire la même
    /// représentation, sans quoi les jointures échoueraient silencieusement.
    /// </summary>
    /// <summary>
    /// Les identifiants sont écrits en texte canonique majuscule à tirets, la même forme que
    /// <c>HostDeckDbContext</c> impose aux colonnes Guid par sa conversion : les deux chemins
    /// d'accès produisent la même représentation, sans quoi les clés étrangères échoueraient
    /// silencieusement.
    /// </summary>
    private static string ToDatabaseId(ServerId serverId) =>
        serverId.Value.ToString("D").ToUpperInvariant();

    private static async Task OpenAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
