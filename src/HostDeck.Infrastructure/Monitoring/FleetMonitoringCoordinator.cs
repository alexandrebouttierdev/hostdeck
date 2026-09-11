// Adaptateur de frontière : les exceptions tierces (SSH.NET, Latchkey, OS) sont
// converties en erreurs Application typées ; CA1031 est donc désactivé ici.
#pragma warning disable CA1031
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Monitoring;

/// <summary>
/// Service de fond qui planifie la collecte de la flotte : concurrence bornée, retry borné,
/// annulation, évaluation d'incidents et publication d'événements (§17).
/// </summary>
internal sealed class FleetMonitoringCoordinator : BackgroundService
{
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly ISshConnectionFactory _ssh;
    private readonly IMetricsRepository _metrics;
    private readonly LinuxMetricCollector _collector;
    private readonly IncidentEvaluationEngine _incidentEngine;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IMonitoringEventBus _events;
    private readonly IClock _clock;
    private readonly ILogger<FleetMonitoringCoordinator> _logger;
    private readonly ConcurrentDictionary<Guid, ServerCollectionState> _states = new();
    private DateTimeOffset? _lastPruneAt;

    public FleetMonitoringCoordinator(
        IServerRepository servers,
        ISettingsRepository settings,
        ISshConnectionFactory ssh,
        IMetricsRepository metrics,
        LinuxMetricCollector collector,
        IncidentEvaluationEngine incidentEngine,
        IIncidentRepository incidentRepository,
        IMonitoringEventBus events,
        IClock clock,
        ILogger<FleetMonitoringCoordinator> logger)
    {
        _servers = servers;
        _settings = settings;
        _ssh = ssh;
        _metrics = metrics;
        _collector = collector;
        _incidentEngine = incidentEngine;
        _incidentRepository = incidentRepository;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        InfrastructureLog.FleetCoordinatorStarted(_logger);

        // Premier cycle immédiat, puis rythme du timer.
        await RunCycleAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arrêt propre.
        }

        InfrastructureLog.FleetCoordinatorStopped(_logger);
    }

    /// <summary>
    /// Exécute un cycle de collecte. Interne pour les tests.
    /// </summary>
    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var servers = await _servers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var due = servers.Where(server => IsDue(server, settings)).ToList();

        if (due.Count == 0)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var succeeded = 0;
        var failed = 0;

        using var gate = new SemaphoreSlim(Math.Max(1, settings.MaxConcurrentCollections));
        var tasks = due.Select(async server =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var ok = await CollectServerWithRetryAsync(server, settings, cancellationToken)
                    .ConfigureAwait(false);
                if (ok)
                {
                    Interlocked.Increment(ref succeeded);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _events.Publish(new CollectionCycleCompletedEvent(
            succeeded,
            failed,
            stopwatch.Elapsed,
            _clock.UtcNow));

        InfrastructureLog.FleetCycleCompleted(
            _logger,
            succeeded,
            failed,
            stopwatch.ElapsedMilliseconds);

        if (succeeded > 0)
        {
            await TryPruneAsync(settings, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryPruneAsync(SettingsSnapshot settings, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (_lastPruneAt is { } lastPrune && now - lastPrune < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastPruneAt = now;
        var policy = settings.ToRetentionPolicy();
        var metricCutoff = policy.MetricCutoff(now);
        var eventCutoff = policy.EventCutoff(now);

        var metricsRemoved = await _metrics
            .PruneOlderThanAsync(metricCutoff, cancellationToken)
            .ConfigureAwait(false);
        if (metricsRemoved > 0)
        {
            InfrastructureLog.RowsPruned(_logger, metricsRemoved, metricCutoff);
        }

        var incidentsRemoved = await _incidentRepository
            .PruneResolvedOlderThanAsync(eventCutoff, cancellationToken)
            .ConfigureAwait(false);
        if (incidentsRemoved > 0)
        {
            InfrastructureLog.RowsPruned(_logger, incidentsRemoved, eventCutoff);
        }
    }

    private bool IsDue(Server server, SettingsSnapshot settings)
    {
        var state = _states.GetOrAdd(server.Id.Value, static _ => new ServerCollectionState());
        if (state.LastAttemptAt is null)
        {
            return true;
        }

        var interval = server.MonitoringInterval.Value;
        if (interval <= TimeSpan.Zero)
        {
            interval = TimeSpan.FromSeconds(settings.DefaultCollectionIntervalSeconds);
        }

        return _clock.UtcNow - state.LastAttemptAt.Value >= interval;
    }

    private async Task<bool> CollectServerWithRetryAsync(
        Server server,
        SettingsSnapshot settings,
        CancellationToken cancellationToken)
    {
        var state = _states.GetOrAdd(server.Id.Value, static _ => new ServerCollectionState());
        var attempts = Math.Max(1, settings.CollectionRetryCount);
        var timeout = TimeSpan.FromSeconds(Math.Max(1, settings.CollectionTimeoutSeconds));

        Exception? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(timeout);
                await CollectServerAsync(server, state, attemptCts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                              || !cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
                InfrastructureLog.FleetCollectAttemptFailed(
                    _logger,
                    exception,
                    server.Id.Value,
                    attempt,
                    attempts);

                if (attempt < attempts)
                {
                    var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }
            }
        }

        await RecordFailureAsync(server, state, lastError, cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async Task CollectServerAsync(
        Server server,
        ServerCollectionState state,
        CancellationToken cancellationToken)
    {
        state.LastAttemptAt = _clock.UtcNow;
        var previousStatus = server.Status;

        await using var connection = await _ssh.ConnectAsync(server, cancellationToken)
            .ConfigureAwait(false);

        var success = await _collector
            .CollectAsync(server, connection, state, cancellationToken)
            .ConfigureAwait(false);

        await _metrics.AddBatchAsync([success.Sample], cancellationToken).ConfigureAwait(false);

        server.UpdateIdentity(success.Identity);
        server.RecordCollection(ServerStatus.Online, success.Sample.ObservedAt);
        await _servers.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

        PublishStatusChange(server.Id, previousStatus, ServerStatus.Online);
        _events.Publish(new MetricUpdatedEvent(server.Id, success.Latest, success.Sample.ObservedAt));

        await _incidentEngine
            .EvaluateAsync(server, success.Sample, ServerStatus.Online, state, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        Server server,
        ServerCollectionState state,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        state.LastAttemptAt = _clock.UtcNow;
        var previousStatus = server.Status;
        var status = MapFailureStatus(exception);
        var observedAt = _clock.UtcNow;

        try
        {
            server.RecordCollection(status, observedAt);
            await _servers.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception updateException) when (updateException is not OperationCanceledException)
        {
            InfrastructureLog.FleetStatusPersistFailed(_logger, updateException, server.Id.Value);
        }

        PublishStatusChange(server.Id, previousStatus, status);

        if (exception is HostKeyVerificationException hostKey && hostKey.IsKeyChange)
        {
            _events.Publish(new HostKeyChangedEvent(
                server.Id,
                hostKey.Host,
                hostKey.PresentedFingerprint,
                hostKey.KnownFingerprint!,
                observedAt));
        }

        try
        {
            await _incidentEngine
                .EvaluateAsync(server, sample: null, status, state, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception evaluateException) when (evaluateException is not OperationCanceledException)
        {
            InfrastructureLog.FleetIncidentEvaluateFailed(_logger, evaluateException, server.Id.Value);
        }
    }

    private void PublishStatusChange(ServerId serverId, ServerStatus previous, ServerStatus current)
    {
        if (previous == current)
        {
            return;
        }

        _events.Publish(new ServerStatusChangedEvent(serverId, previous, current, _clock.UtcNow));
    }

    private static ServerStatus MapFailureStatus(Exception? exception) => exception switch
    {
        HostKeyVerificationException => ServerStatus.HostKeyRejected,
        GatewayUnavailableException => ServerStatus.GatewayUnavailable,
        SshAuthenticationException => ServerStatus.AuthenticationFailed,
        CredentialException => ServerStatus.AuthenticationFailed,
        _ => ServerStatus.Offline,
    };
}
