using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Servers;
using HostDeck.Presentation.Charts;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Infrastructure;

namespace HostDeck.Presentation.ViewModels.Overview;

/// <summary>
/// Vue d'ensemble : KPIs, cadres graphiques et top hôtes.
/// Flotte vide → états vides, jamais de séries inventées.
/// </summary>
public partial class OverviewViewModel : PageViewModelBase, IDisposable
{
    private readonly GetServersUseCase _getServers;
    private readonly GetMetricHistoryUseCase _getHistory;
    private readonly IMonitoringEventBus _events;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _hostCount;

    [ObservableProperty]
    private int _activeIncidentCount;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _cpuSeries;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _memorySeries;

    public ObservableCollection<HostListItemViewModel> TopHosts { get; } = [];

    public OverviewViewModel(
        GetServersUseCase getServers,
        GetMetricHistoryUseCase getHistory,
        IMonitoringEventBus events,
        IDialogService dialogs,
        IUiDispatcher ui)
    {
        _getServers = getServers;
        _getHistory = getHistory;
        _events = events;
        _dialogs = dialogs;
        _ui = ui;

        _subscriptions.Add(_events.Subscribe<MetricUpdatedEvent>(OnMetricUpdated));
        _subscriptions.Add(_events.Subscribe<IncidentChangedEvent>(OnIncidentChanged));
        _subscriptions.Add(_events.Subscribe<CollectionCycleCompletedEvent>(OnCycleCompleted));
    }

    public override string Title => "Vue d’ensemble";

    public override string Breadcrumb => "Infrastructure › Vue d’ensemble";

    public override string StatusSummary =>
        HostCount == 0 ? "0 hôte" : $"{HostCount} hôte{(HostCount > 1 ? "s" : string.Empty)}";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        HostCount = servers.Count;
        IsEmpty = HostCount == 0;

        TopHosts.Clear();
        foreach (var server in servers.Take(8))
        {
            var series = await LoadSparklineAsync(server.ServerId, cancellationToken).ConfigureAwait(true);
            TopHosts.Add(new HostListItemViewModel(server, networkSeries: series));
        }

        // Pas d'agrégat flotte côté Application : les graphiques globaux restent vides
        // tant qu'aucun use case d'agrégation n'existe (ne pas inventer de moyenne).
        CpuSeries = null;
        MemorySeries = null;

        OnPropertyChanged(nameof(StatusSummary));
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
        });
    }

    private void OnCycleCompleted(CollectionCycleCompletedEvent evt)
    {
        _ui.Post(() => OnPropertyChanged(nameof(StatusSummary)));
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
