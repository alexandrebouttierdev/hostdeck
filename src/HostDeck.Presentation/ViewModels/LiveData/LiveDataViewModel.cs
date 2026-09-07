using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Servers;
using HostDeck.Presentation.Charts;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Infrastructure;

namespace HostDeck.Presentation.ViewModels.LiveData;

/// <summary>Métriques temps réel. Sans flotte / sans sélection : empty state + cadres vides.</summary>
public partial class LiveDataViewModel : PageViewModelBase, IDisposable
{
    private readonly GetServersUseCase _getServers;
    private readonly GetMetricHistoryUseCase _getHistory;
    private readonly IMonitoringEventBus _events;
    private readonly IUiDispatcher _ui;
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private HostListItemViewModel? _selectedHost;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _cpuSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _memorySeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _diskSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _networkSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _loadSeries;

    public ObservableCollection<HostListItemViewModel> Hosts { get; } = [];

    public LiveDataViewModel(
        GetServersUseCase getServers,
        GetMetricHistoryUseCase getHistory,
        IMonitoringEventBus events,
        IUiDispatcher ui)
    {
        _getServers = getServers;
        _getHistory = getHistory;
        _events = events;
        _ui = ui;
        _subscriptions.Add(_events.Subscribe<MetricUpdatedEvent>(OnMetricUpdated));
    }

    public override string Title => "Données en direct";

    public override string Breadcrumb => "Supervision › Données en direct";

    public override string StatusSummary =>
        SelectedHost is null ? "Aucune série" : SelectedHost.Name;

    public bool HasSelection => SelectedHost is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var previousId = SelectedHost?.Server.ServerId;
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Hosts.Clear();
        HostListItemViewModel? restored = null;
        foreach (var server in servers)
        {
            var row = new HostListItemViewModel(server);
            Hosts.Add(row);
            if (previousId is Guid id && server.ServerId == id)
            {
                restored = row;
            }
        }

        IsEmpty = Hosts.Count == 0;
        SelectedHost = IsEmpty ? null : restored ?? (Hosts.Count > 0 ? Hosts[0] : null);
        await LoadSelectedHistoryAsync(cancellationToken).ConfigureAwait(true);

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSelectedHostChanged(HostListItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(StatusSummary));
        _ = LoadSelectedHistoryAsync();
    }

    private async Task LoadSelectedHistoryAsync(CancellationToken cancellationToken = default)
    {
        ClearSeries();
        if (SelectedHost is null)
        {
            return;
        }

        var to = DateTimeOffset.UtcNow;
        var history = await _getHistory.ExecuteAsync(
            new MetricHistoryRequestDto
            {
                ServerId = SelectedHost.Server.ServerId,
                From = to.AddHours(-1),
                To = to,
                MaxPoints = 600,
                Series =
                [
                    MetricSeriesKind.CpuTotal,
                    MetricSeriesKind.MemoryUsed,
                    MetricSeriesKind.DiskUsed,
                    MetricSeriesKind.NetworkReceived,
                    MetricSeriesKind.NetworkTransmitted,
                    MetricSeriesKind.LoadOne,
                ],
            },
            cancellationToken).ConfigureAwait(true);

        CpuSeries = Wrap(MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.CpuTotal), "CPU", MetricSeriesMapper.CpuColor));
        MemorySeries = Wrap(MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.MemoryUsed), "Mémoire", MetricSeriesMapper.MemoryColor));
        DiskSeries = Wrap(MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.DiskUsed), "Disque", MetricSeriesMapper.DiskColor));
        LoadSeries = Wrap(MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.LoadOne), "Load 1m", MetricSeriesMapper.LoadColor));

        var netIn = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkReceived), "Entrant", MetricSeriesMapper.NetworkInColor);
        var netOut = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkTransmitted), "Sortant", MetricSeriesMapper.NetworkOutColor);
        NetworkSeries = BuildMulti(netIn, netOut);
    }

    private void OnMetricUpdated(MetricUpdatedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = Hosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyMetric(evt.Metric);

            if (SelectedHost is null || SelectedHost.Server.ServerId != evt.ServerId.Value)
            {
                return;
            }

            CpuSeries = Append(CpuSeries, "CPU", MetricSeriesMapper.CpuColor, (float)evt.Metric.CpuPercent);
            MemorySeries = Append(MemorySeries, "Mémoire", MetricSeriesMapper.MemoryColor, (float)evt.Metric.MemoryPercent);
            DiskSeries = Append(DiskSeries, "Disque", MetricSeriesMapper.DiskColor, (float)evt.Metric.DiskPercent);
            LoadSeries = Append(LoadSeries, "Load 1m", MetricSeriesMapper.LoadColor, (float)evt.Metric.LoadOneMinute);
        });
    }

    private static IReadOnlyList<ChartSeriesData>? Wrap(ChartSeriesData? series)
        => series is null ? null : [series];

    private static ChartSeriesData[]? BuildMulti(params ChartSeriesData?[] series)
    {
        var list = series.Where(item => item is not null).Cast<ChartSeriesData>().ToArray();
        return list.Length == 0 ? null : list;
    }

    private static IReadOnlyList<ChartSeriesData>? Append(
        IReadOnlyList<ChartSeriesData>? current,
        string name,
        Avalonia.Media.Color color,
        float value)
    {
        var existing = current is { Count: > 0 } ? current[0].Values : null;
        var values = MetricSeriesMapper.AppendBounded(existing, value, 600);
        return values.Count < 2
            ? null
            :
            [
                new ChartSeriesData
                {
                    Name = name,
                    Values = values,
                    Color = color,
                },
            ];
    }

    private void ClearSeries()
    {
        CpuSeries = null;
        MemorySeries = null;
        DiskSeries = null;
        NetworkSeries = null;
        LoadSeries = null;
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
