using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Charts;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.Services;

namespace HostDeck.Presentation.ViewModels.HostDetails;

/// <summary>
/// Détail d'un hôte. Sans sélection : panneau vide. Les graphiques hors historique réel
/// restent en cadre + empty state.
/// </summary>
public partial class HostDetailsViewModel : PageViewModelBase, IDisposable
{
    private readonly GetMetricHistoryUseCase _getHistory;
    private readonly IMonitoringEventBus _events;
    private readonly IUiDispatcher _ui;
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    [ObservableProperty]
    private ServerSummaryDto? _selectedHost;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _cpuSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _memorySeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _diskSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _networkSeries;

    public HostDetailsViewModel(
        GetMetricHistoryUseCase getHistory,
        IMonitoringEventBus events,
        IUiDispatcher ui)
    {
        _getHistory = getHistory;
        _events = events;
        _ui = ui;
        _subscriptions.Add(_events.Subscribe<MetricUpdatedEvent>(OnMetricUpdated));
    }

    public override string Title => "Détail de l’hôte";

    public string DisplayName => SelectedHost?.Name ?? "Aucun hôte sélectionné";

    public string HostNameOrDash => SelectedHost?.Name ?? "—";

    public string DisplayAddress => SelectedHost?.Address ?? "—";

    public string DisplayOperatingSystem => SelectedHost?.OperatingSystem ?? "—";

    public string DisplayStatus => SelectedHost is null
        ? "—"
        : SelectedHost.Status switch
        {
            ServerStatus.Unknown => "Inconnu",
            ServerStatus.Online => "En ligne",
            ServerStatus.Offline => "Hors ligne",
            ServerStatus.GatewayUnavailable => "Bastion indisponible",
            ServerStatus.AuthenticationFailed => "Authentification refusée",
            ServerStatus.HostKeyRejected => "Clé d'hôte refusée",
            _ => SelectedHost.Status.ToString(),
        };


    public override string Breadcrumb =>
        HasSelection && SelectedHost is not null
            ? $"Infrastructure › {SelectedHost.Name}"
            : "Infrastructure › Détail de l’hôte";

    public override string StatusSummary =>
        HasSelection && SelectedHost is not null ? SelectedHost.Name : "Aucun hôte sélectionné";

    public async Task ShowHostAsync(ServerSummaryDto? host, CancellationToken cancellationToken = default)
    {
        SelectedHost = host;
        HasSelection = host is not null;
        ClearSeries();

        if (host is not null)
        {
            await LoadHistoryAsync(host.ServerId, cancellationToken).ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HostNameOrDash));
        OnPropertyChanged(nameof(DisplayAddress));
        OnPropertyChanged(nameof(DisplayOperatingSystem));
        OnPropertyChanged(nameof(DisplayStatus));
    }

    /// <summary>Compatibilité shell synchrone : charge l'historique en tâche de fond UI.</summary>
    public void ShowHost(ServerSummaryDto? host)
    {
        _ = ShowHostAsync(host);
    }

    private async Task LoadHistoryAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var history = await _getHistory.ExecuteAsync(
            new MetricHistoryRequestDto
            {
                ServerId = serverId,
                From = to.AddHours(-6),
                To = to,
                MaxPoints = 600,
                Series =
                [
                    ..MetricSeriesMapper.CpuStackKinds,
                    MetricSeriesKind.CpuTotal,
                    MetricSeriesKind.MemoryUsed,
                    MetricSeriesKind.DiskUsed,
                    MetricSeriesKind.NetworkReceived,
                    MetricSeriesKind.NetworkTransmitted,
                ],
            },
            cancellationToken).ConfigureAwait(true);

        CpuSeries = MetricSeriesMapper.ToCpuStackSeries(history)
            ?? Wrap(MetricSeriesMapper.ToChartSeries(
                MetricSeriesMapper.Find(history, MetricSeriesKind.CpuTotal), "CPU", MetricSeriesMapper.CpuColor));
        var memory = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.MemoryUsed), "Mémoire", MetricSeriesMapper.MemoryColor);
        var disk = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.DiskUsed), "Disque", MetricSeriesMapper.DiskColor);

        var netIn = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkReceived), "Entrant", MetricSeriesMapper.NetworkInColor);
        var netOut = MetricSeriesMapper.ToChartSeries(
            MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkTransmitted), "Sortant", MetricSeriesMapper.NetworkOutColor);

        MemorySeries = Wrap(memory);
        DiskSeries = Wrap(disk);
        NetworkSeries = BuildMulti(netIn, netOut);
    }

    private void OnMetricUpdated(MetricUpdatedEvent evt)
    {
        if (SelectedHost is null || SelectedHost.ServerId != evt.ServerId.Value)
        {
            return;
        }

        _ui.Post(() =>
        {
            SelectedHost = SelectedHost with
            {
                Latest = evt.Metric,
                LastCollectedAt = evt.Metric.ObservedAt,
            };

            // Latest ne porte pas les modes CPU : on n'écrase pas un empilement historique.
            if (CpuSeries is not { Count: > 1 })
            {
                CpuSeries = AppendChart(CpuSeries, "CPU", MetricSeriesMapper.CpuColor, (float)evt.Metric.CpuPercent);
            }

            MemorySeries = AppendChart(MemorySeries, "Mémoire", MetricSeriesMapper.MemoryColor, (float)evt.Metric.MemoryPercent);
            DiskSeries = AppendChart(DiskSeries, "Disque", MetricSeriesMapper.DiskColor, (float)evt.Metric.DiskPercent);
            OnPropertyChanged(nameof(StatusSummary));
        });
    }

    private static IReadOnlyList<ChartSeriesData>? AppendChart(
        IReadOnlyList<ChartSeriesData>? current,
        string name,
        Avalonia.Media.Color color,
        float value,
        DateTimeOffset? timestamp = null)
    {
        if (current is { Count: > 1 })
        {
            return current;
        }

        var existing = current is { Count: > 0 } ? current[0].Values : null;
        var existingTs = current is { Count: > 0 } ? current[0].Timestamps : null;
        var values = MetricSeriesMapper.AppendBounded(existing, value, 600);
        if (values.Count < 2)
        {
            return null;
        }

        IReadOnlyList<DateTimeOffset>? timestamps = null;
        if (timestamp is not null || existingTs is not null)
        {
            timestamps = MetricSeriesMapper.AppendBoundedTimestamps(
                existingTs,
                timestamp ?? DateTimeOffset.UtcNow,
                600);
        }

        return
        [
            new ChartSeriesData
            {
                Name = name,
                Values = values,
                Color = color,
                Timestamps = timestamps,
            },
        ];
    }

    private static IReadOnlyList<ChartSeriesData>? Wrap(ChartSeriesData? series)
        => series is null ? null : [series];

    private static ChartSeriesData[]? BuildMulti(params ChartSeriesData?[] series)
    {
        var list = series.Where(item => item is not null).Cast<ChartSeriesData>().ToArray();
        return list.Length == 0 ? null : list;
    }

    private void ClearSeries()
    {
        CpuSeries = null;
        MemorySeries = null;
        DiskSeries = null;
        NetworkSeries = null;
    }

    partial void OnSelectedHostChanged(ServerSummaryDto? value)
    {
        HasSelection = value is not null;
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HostNameOrDash));
        OnPropertyChanged(nameof(DisplayAddress));
        OnPropertyChanged(nameof(DisplayOperatingSystem));
        OnPropertyChanged(nameof(DisplayStatus));
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
