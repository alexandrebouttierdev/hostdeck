using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Incidents;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Servers;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Charts;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Infrastructure;

namespace HostDeck.Presentation.ViewModels.Overview;

/// <summary>
/// Vue d'ensemble : KPIs, cadres graphiques et top hôtes.
/// Les agrégats flotte sont calculés uniquement à partir des Latest / historiques réels
/// (moyenne des hôtes qui ont des données) — jamais de séries inventées.
/// </summary>
public partial class OverviewViewModel : PageViewModelBase, IDisposable
{
    /// <summary>
    /// Échelle honnête pour la jauge réseau (%) : 100 % = <see cref="NetworkGaugeReferenceMbps"/> Mbit/s
    /// (somme RX+TX flotte). Documentée ici car un « % » réseau n'a pas de sens absolu.
    /// </summary>
    public const double NetworkGaugeReferenceMbps = 1000d;

    private const int FleetChartMaxPoints = 600;

    private readonly GetServersUseCase _getServers;
    private readonly GetIncidentsUseCase _getIncidents;
    private readonly GetMetricHistoryUseCase _getHistory;
    private readonly IMonitoringEventBus _events;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly List<IDisposable> _subscriptions = [];
    private IReadOnlyList<ServerSummaryDto> _servers = [];
    private bool _disposed;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _hostCount;

    [ObservableProperty]
    private int _onlineHostCount;

    [ObservableProperty]
    private int _warningHostCount;

    [ObservableProperty]
    private int _problemHostCount;

    [ObservableProperty]
    private int _activeIncidentCount;

    [ObservableProperty]
    private bool _hasRecentIncidents;

    [ObservableProperty]
    private double? _cpuGaugeValue;

    [ObservableProperty]
    private double? _memoryGaugeValue;

    [ObservableProperty]
    private double? _diskGaugeValue;

    [ObservableProperty]
    private double? _networkGaugeValue;

    [ObservableProperty]
    private double? _availabilityPercent;

    [ObservableProperty]
    private string _hostsStatusSubtitle = "0 opérationnel · 0 avertissement · 0 problème";

    [ObservableProperty]
    private string _hostsOnlineDetailText = "0 opérationnels";

    [ObservableProperty]
    private string _hostsWarningDetailText = "0 avec avertissements";

    [ObservableProperty]
    private string _hostsProblemDetailText = "0 avec problèmes";

    [ObservableProperty]
    private string _incidentsSubtitle = "Aucun incident ouvert";

    [ObservableProperty]
    private string _alertsDetailText = "Aucune alerte";

    [ObservableProperty]
    private string _availabilityText = "—";

    [ObservableProperty]
    private string _availabilitySubtitle = "Indisponible sans flotte";

    [ObservableProperty]
    private string _availabilityOnlineDetailText = "0 / 0 en ligne";

    [ObservableProperty]
    private string _availabilityUnknownDetailText = "0 inconnus";

    [ObservableProperty]
    private string _availabilityOfflineDetailText = "0 hors ligne";

    [ObservableProperty]
    private string _healthStatusTitle = "Collecte prête";

    [ObservableProperty]
    private string _collectionDetailText = "En attente du premier hôte.";

    [ObservableProperty]
    private string _cpuCoresText = "Cœurs : —";

    [ObservableProperty]
    private string _cpuAverageText = "Moyenne : —";

    [ObservableProperty]
    private string _cpuPeakText = "Pic : —";

    [ObservableProperty]
    private string _memoryUsedText = "Utilisée : —";

    [ObservableProperty]
    private string _memoryTotalText = "Totale : —";

    [ObservableProperty]
    private string _memoryPeakText = "Pic : —";

    [ObservableProperty]
    private string _diskUsedText = "Utilisé : —";

    [ObservableProperty]
    private string _diskTotalText = "Total : —";

    [ObservableProperty]
    private string _diskPeakText = "Pic : —";

    [ObservableProperty]
    private string _networkInText = "Entrant : —";

    [ObservableProperty]
    private string _networkOutText = "Sortant : —";

    [ObservableProperty]
    private string _networkPeakText = "Pic : —";

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _cpuSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _memorySeries;

    public ObservableCollection<HostListItemViewModel> TopHosts { get; } = [];

    public ObservableCollection<IncidentDto> RecentIncidents { get; } = [];

    public OverviewViewModel(
        GetServersUseCase getServers,
        GetIncidentsUseCase getIncidents,
        GetMetricHistoryUseCase getHistory,
        IMonitoringEventBus events,
        IDialogService dialogs,
        IUiDispatcher ui)
    {
        _getServers = getServers;
        _getIncidents = getIncidents;
        _getHistory = getHistory;
        _events = events;
        _dialogs = dialogs;
        _ui = ui;

        _subscriptions.Add(_events.Subscribe<MetricUpdatedEvent>(OnMetricUpdated));
        _subscriptions.Add(_events.Subscribe<IncidentChangedEvent>(OnIncidentChanged));
        _subscriptions.Add(_events.Subscribe<CollectionCycleCompletedEvent>(OnCycleCompleted));
        _subscriptions.Add(_events.Subscribe<ServerStatusChangedEvent>(OnServerStatusChanged));
    }

    public override string Title => "Vue d’ensemble";

    public override string Breadcrumb => "Infrastructure › Vue d’ensemble";

    public override string StatusSummary =>
        HostCount == 0 ? "0 hôte" : $"{HostCount} hôte{(HostCount > 1 ? "s" : string.Empty)}";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        _servers = servers;
        HostCount = servers.Count;
        IsEmpty = HostCount == 0;

        TopHosts.Clear();
        foreach (var server in servers.Take(8))
        {
            var series = await LoadSparklineAsync(server.ServerId, cancellationToken).ConfigureAwait(true);
            TopHosts.Add(new HostListItemViewModel(server, networkSeries: series));
        }

        ApplyAggregates(servers);
        await LoadFleetChartsAsync(servers, cancellationToken).ConfigureAwait(true);
        await LoadIncidentsAsync(cancellationToken).ConfigureAwait(true);

        OnPropertyChanged(nameof(StatusSummary));
    }

    private async Task LoadIncidentsAsync(CancellationToken cancellationToken)
    {
        var incidents = await _getIncidents.ExecuteAsync(
            new IncidentFilterDto
            {
                Statuses = [IncidentStatus.Open, IncidentStatus.Acknowledged],
            },
            cancellationToken).ConfigureAwait(true);

        ActiveIncidentCount = incidents.Count;
        IncidentsSubtitle = ActiveIncidentCount == 0
            ? "Aucun incident ouvert"
            : $"{ActiveIncidentCount} incident{(ActiveIncidentCount > 1 ? "s" : string.Empty)} ouvert{(ActiveIncidentCount > 1 ? "s" : string.Empty)}";

        RecentIncidents.Clear();
        foreach (var incident in incidents.Take(5))
        {
            RecentIncidents.Add(incident);
        }

        HasRecentIncidents = RecentIncidents.Count > 0;
    }

    [RelayCommand]
    private async Task AddHostAsync(CancellationToken cancellationToken)
    {
        var added = await _dialogs.ShowAddHostAsync(cancellationToken).ConfigureAwait(true);
        if (added)
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private void OnMetricUpdated(MetricUpdatedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = TopHosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyMetric(evt.Metric);

            _servers =
            [
                .. _servers.Select(server =>
                    server.ServerId == evt.ServerId.Value
                        ? server with
                        {
                            Latest = evt.Metric,
                            LastCollectedAt = evt.Metric.ObservedAt,
                            Status = ServerStatus.Online,
                        }
                        : server),
            ];

            ApplyAggregates(_servers);
        });
    }

    private void OnServerStatusChanged(ServerStatusChangedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = TopHosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyStatus(evt.Current);

            _servers =
            [
                .. _servers.Select(server =>
                    server.ServerId == evt.ServerId.Value
                        ? server with { Status = evt.Current }
                        : server),
            ];

            ApplyAggregates(_servers);
        });
    }

    private void OnIncidentChanged(IncidentChangedEvent evt)
    {
        _ui.Post(() =>
        {
            if (evt.Change is IncidentChangeKind.Opened)
            {
                ActiveIncidentCount++;
            }
            else if (evt.Change is IncidentChangeKind.Resolved or IncidentChangeKind.Recovered)
            {
                ActiveIncidentCount = Math.Max(0, ActiveIncidentCount - 1);
            }

            IncidentsSubtitle = ActiveIncidentCount == 0
                ? "Aucun incident ouvert"
                : $"{ActiveIncidentCount} incident{(ActiveIncidentCount > 1 ? "s" : string.Empty)} ouvert{(ActiveIncidentCount > 1 ? "s" : string.Empty)}";
        });
    }

    private void OnCycleCompleted(CollectionCycleCompletedEvent evt)
    {
        _ui.Post(() => OnPropertyChanged(nameof(StatusSummary)));
    }

    /// <summary>
    /// Agrégats flotte depuis les Latest réels uniquement.
    /// CPU / mémoire / disque = moyenne des hôtes qui ont un Latest ;
    /// pic = max de ces Latest ; réseau = somme des débits RX/TX.
    /// </summary>
    private void ApplyAggregates(IReadOnlyList<ServerSummaryDto> servers)
    {
        HostCount = servers.Count;
        IsEmpty = HostCount == 0;

        OnlineHostCount = servers.Count(server => server.Status == ServerStatus.Online);
        WarningHostCount = servers.Count(server => server.Status == ServerStatus.Unknown);
        ProblemHostCount = servers.Count(server =>
            server.Status is ServerStatus.Offline
                or ServerStatus.GatewayUnavailable
                or ServerStatus.AuthenticationFailed
                or ServerStatus.HostKeyRejected);

        HostsStatusSubtitle =
            $"{OnlineHostCount} opérationnel · {WarningHostCount} avertissement · {ProblemHostCount} problème";

        HostsOnlineDetailText = FormatHostDetail(OnlineHostCount, "opérationnel", "opérationnels");
        HostsWarningDetailText = FormatHostDetail(WarningHostCount, "avec avertissement", "avec avertissements");
        HostsProblemDetailText = FormatHostDetail(ProblemHostCount, "avec problème", "avec problèmes");

        CollectionDetailText = HostCount == 0
            ? "En attente du premier hôte."
            : $"{HostCount} hôte{(HostCount > 1 ? "s" : string.Empty)} sous surveillance.";

        HealthStatusTitle = HostCount == 0
            ? "Collecte prête"
            : ProblemHostCount > 0
                ? "Attention requise"
                : "Tout est opérationnel";

        if (HostCount == 0)
        {
            AvailabilityPercent = null;
            AvailabilityText = "—";
            AvailabilitySubtitle = "Indisponible sans flotte";
            AvailabilityOnlineDetailText = "0 / 0 en ligne";
            AvailabilityUnknownDetailText = FormatHostDetail(0, "inconnu", "inconnus");
            AvailabilityOfflineDetailText = FormatHostDetail(0, "hors ligne", "hors ligne");
        }
        else
        {
            AvailabilityPercent = 100d * OnlineHostCount / HostCount;
            AvailabilityText = string.Create(
                CultureInfo.CurrentCulture,
                $"{AvailabilityPercent.Value:0.#} %");
            AvailabilitySubtitle = string.Create(
                CultureInfo.CurrentCulture,
                $"{OnlineHostCount}/{HostCount} en ligne");
            AvailabilityOnlineDetailText = string.Create(
                CultureInfo.CurrentCulture,
                $"{OnlineHostCount} / {HostCount} en ligne");
            AvailabilityUnknownDetailText = FormatHostDetail(WarningHostCount, "inconnu", "inconnus");
            AvailabilityOfflineDetailText = FormatHostDetail(ProblemHostCount, "hors ligne", "hors ligne");
        }

        var withLatest = servers.Where(server => server.Latest is not null).Select(server => server.Latest!).ToList();
        if (withLatest.Count == 0)
        {
            ClearMetricAggregates();
            return;
        }

        var cpuAvg = withLatest.Average(metric => metric.CpuPercent);
        var cpuPeak = withLatest.Max(metric => metric.CpuPercent);
        CpuGaugeValue = cpuAvg;
        CpuCoresText = "Cœurs : —";
        CpuAverageText = FormatPercentLabel("Moyenne", cpuAvg);
        CpuPeakText = FormatPercentLabel("Pic", cpuPeak);

        var memAvg = withLatest.Average(metric => metric.MemoryPercent);
        var memPeak = withLatest.Max(metric => metric.MemoryPercent);
        MemoryGaugeValue = memAvg;
        // Pas de totaux absolus dans LatestMetricDto : on reste honnête.
        MemoryUsedText = FormatPercentLabel("Moyenne", memAvg);
        MemoryTotalText = "Totale : —";
        MemoryPeakText = FormatPercentLabel("Pic", memPeak);

        var diskAvg = withLatest.Average(metric => metric.DiskPercent);
        var diskPeak = withLatest.Max(metric => metric.DiskPercent);
        DiskGaugeValue = diskAvg;
        DiskUsedText = FormatPercentLabel("Moyenne", diskAvg);
        DiskTotalText = "Total : —";
        DiskPeakText = FormatPercentLabel("Pic", diskPeak);

        var rxSum = withLatest.Sum(metric => metric.NetworkReceivedBytesPerSecond);
        var txSum = withLatest.Sum(metric => metric.NetworkTransmittedBytesPerSecond);
        var peakHostThroughput = withLatest.Max(
            metric => metric.NetworkReceivedBytesPerSecond + metric.NetworkTransmittedBytesPerSecond);

        NetworkInText = "Entrant : " + FormatBytesPerSecond(rxSum);
        NetworkOutText = "Sortant : " + FormatBytesPerSecond(txSum);
        NetworkPeakText = "Pic : " + FormatBytesPerSecond(peakHostThroughput);

        // Jauge % bornée : min(100, totalMbps / référence × 100), référence = 1 Gbit/s.
        var totalMbps = (rxSum + txSum) * 8d / 1_000_000d;
        NetworkGaugeValue = Math.Min(100d, totalMbps / NetworkGaugeReferenceMbps * 100d);
    }

    private static string FormatHostDetail(int count, string singular, string plural)
        => count <= 1
            ? $"{count} {singular}"
            : $"{count} {plural}";

    private void ClearMetricAggregates()
    {
        CpuGaugeValue = null;
        MemoryGaugeValue = null;
        DiskGaugeValue = null;
        NetworkGaugeValue = null;
        CpuCoresText = "Cœurs : —";
        CpuAverageText = "Moyenne : —";
        CpuPeakText = "Pic : —";
        MemoryUsedText = "Utilisée : —";
        MemoryTotalText = "Totale : —";
        MemoryPeakText = "Pic : —";
        DiskUsedText = "Utilisé : —";
        DiskTotalText = "Total : —";
        DiskPeakText = "Pic : —";
        NetworkInText = "Entrant : —";
        NetworkOutText = "Sortant : —";
        NetworkPeakText = "Pic : —";
        CpuSeries = null;
        MemorySeries = null;
    }

    private async Task LoadFleetChartsAsync(
        IReadOnlyList<ServerSummaryDto> servers,
        CancellationToken cancellationToken)
    {
        var candidates = servers.Where(server => server.Latest is not null).ToList();
        if (candidates.Count == 0)
        {
            CpuSeries = null;
            MemorySeries = null;
            return;
        }

        var to = DateTimeOffset.UtcNow;
        var from = to.AddHours(-6);
        var requestedSeries = new List<MetricSeriesKind>(MetricSeriesMapper.CpuStackKinds)
        {
            MetricSeriesKind.CpuTotal,
            MetricSeriesKind.MemoryUsed,
        };

        var cpuModeHistories = new Dictionary<MetricSeriesKind, List<MetricSeriesDto>>();
        foreach (var kind in MetricSeriesMapper.CpuStackKinds)
        {
            cpuModeHistories[kind] = [];
        }

        var cpuTotalHistories = new List<MetricSeriesDto>();
        var memoryHistories = new List<MetricSeriesDto>();

        foreach (var server in candidates)
        {
            var history = await _getHistory.ExecuteAsync(
                new MetricHistoryRequestDto
                {
                    ServerId = server.ServerId,
                    From = from,
                    To = to,
                    MaxPoints = FleetChartMaxPoints,
                    Series = requestedSeries,
                },
                cancellationToken).ConfigureAwait(true);

            foreach (var kind in MetricSeriesMapper.CpuStackKinds)
            {
                var mode = MetricSeriesMapper.Find(history, kind);
                if (mode is { Points.Count: > 0 })
                {
                    cpuModeHistories[kind].Add(mode);
                }
            }

            var cpuTotal = MetricSeriesMapper.Find(history, MetricSeriesKind.CpuTotal);
            if (cpuTotal is { Points.Count: > 0 })
            {
                cpuTotalHistories.Add(cpuTotal);
            }

            var memory = MetricSeriesMapper.Find(history, MetricSeriesKind.MemoryUsed);
            if (memory is { Points.Count: > 0 })
            {
                memoryHistories.Add(memory);
            }
        }

        // Aires empilées si au moins un mode CPU a de l'historique réel ; sinon CpuTotal mono-série.
        var stacked = new List<ChartSeriesData>(MetricSeriesMapper.CpuStackKinds.Length);
        foreach (var kind in MetricSeriesMapper.CpuStackKinds)
        {
            var averaged = AverageSeries(cpuModeHistories[kind], kind);
            var chart = MetricSeriesMapper.ToChartSeries(
                averaged,
                MetricSeriesMapper.DisplayNameForKind(kind),
                MetricSeriesMapper.ColorForKind(kind));
            if (chart is not null)
            {
                stacked.Add(chart);
            }
        }

        if (stacked.Count > 0)
        {
            CpuSeries = stacked;
        }
        else
        {
            var cpuChart = MetricSeriesMapper.ToChartSeries(
                AverageSeries(cpuTotalHistories, MetricSeriesKind.CpuTotal),
                candidates.Count == 1 ? "CPU" : "CPU flotte",
                MetricSeriesMapper.CpuColor);
            CpuSeries = cpuChart is null ? null : [cpuChart];
        }

        var memoryChart = MetricSeriesMapper.ToChartSeries(
            AverageSeries(memoryHistories, MetricSeriesKind.MemoryUsed),
            candidates.Count == 1 ? "Mémoire" : "Mémoire flotte",
            MetricSeriesMapper.MemoryColor);

        MemorySeries = memoryChart is null ? null : [memoryChart];
    }

    /// <summary>
    /// Moyenne des séries par horodatage (bucket). Un seul hôte → sa série telle quelle.
    /// </summary>
    private static MetricSeriesDto? AverageSeries(
        List<MetricSeriesDto> seriesList,
        MetricSeriesKind kind)
    {
        if (seriesList.Count == 0)
        {
            return null;
        }

        if (seriesList.Count == 1)
        {
            return seriesList[0];
        }

        var grouped = seriesList
            .SelectMany(series => series.Points)
            .GroupBy(point => point.Timestamp)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var points = group.ToList();
                return new MetricPointDto(
                    group.Key,
                    points.Min(point => point.Minimum),
                    points.Average(point => point.Average),
                    points.Max(point => point.Maximum));
            })
            .ToArray();

        if (grouped.Length == 0)
        {
            return null;
        }

        return new MetricSeriesDto
        {
            Kind = kind,
            Points = grouped,
        };
    }

    private async Task<IReadOnlyList<float>?> LoadSparklineAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var history = await _getHistory.ExecuteAsync(
            new MetricHistoryRequestDto
            {
                ServerId = serverId,
                From = to.AddHours(-1),
                To = to,
                MaxPoints = HostListItemViewModel.SparklineCapacity,
                Series = [MetricSeriesKind.NetworkReceived],
            },
            cancellationToken).ConfigureAwait(true);

        return MetricSeriesMapper.ToValues(MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkReceived));
    }

    private static string FormatPercentLabel(string prefix, double value)
        => string.Create(CultureInfo.CurrentCulture, $"{prefix} : {value:0}%");

    private static string FormatBytesPerSecond(double bytesPerSecond)
    {
        var sign = bytesPerSecond < 0 ? "-" : string.Empty;
        var value = Math.Abs(bytesPerSecond);
        string[] units = ["B/s", "KiB/s", "MiB/s", "GiB/s"];
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return string.Create(
            CultureInfo.CurrentCulture,
            $"{sign}{value:0.##} {units[unitIndex]}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }
}
