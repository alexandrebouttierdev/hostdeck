using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Servers;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Charts;
using HostDeck.Presentation.Services;

namespace HostDeck.Presentation.ViewModels.Infrastructure;

/// <summary>
/// Inventaire dense des hôtes + panneau détail à la sélection.
/// Sans serveur : état vide. Sans sélection : panneau non rendu.
/// </summary>
public partial class InfrastructureViewModel : PageViewModelBase, IDisposable
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
    private HostListItemViewModel? _selectedHost;

    public ObservableCollection<HostListItemViewModel> Hosts { get; } = [];

    /// <summary>Branché par le shell pour ouvrir Host Details sans coupler les couches.</summary>
    public Func<ServerSummaryDto, CancellationToken, Task>? OpenHostDetailsHandler { get; set; }

    public InfrastructureViewModel(
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
        _subscriptions.Add(_events.Subscribe<ServerStatusChangedEvent>(OnStatusChanged));
        _subscriptions.Add(_events.Subscribe<HostKeyChangedEvent>(OnHostKeyChanged));
    }

    public override string Title => "Infrastructure";

    public override string Breadcrumb => "Infrastructure › Tous les hôtes";

    public override string StatusSummary =>
        Hosts.Count == 0
            ? "0 hôte"
            : $"{Hosts.Count} hôte{(Hosts.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedHost is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var previousId = SelectedHost?.Server.ServerId;

        Hosts.Clear();
        HostListItemViewModel? restored = null;
        foreach (var server in servers)
        {
            var series = await LoadSparklineAsync(server.ServerId, cancellationToken).ConfigureAwait(true);
            var row = new HostListItemViewModel(
                server,
                series.Cpu,
                series.Memory,
                series.Disk,
                series.Network);
            Hosts.Add(row);
            if (previousId is Guid id && server.ServerId == id)
            {
                restored = row;
            }
        }

        IsEmpty = Hosts.Count == 0;
        SelectedHost = IsEmpty ? null : restored;
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
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

    [RelayCommand(CanExecute = nameof(CanOpenHostDetails))]
    private Task OpenHostDetailsAsync(CancellationToken cancellationToken)
    {
        if (SelectedHost is null || OpenHostDetailsHandler is null)
        {
            return Task.CompletedTask;
        }

        return OpenHostDetailsHandler(SelectedHost.Server, cancellationToken);
    }

    private bool CanOpenHostDetails()
        => SelectedHost is not null && OpenHostDetailsHandler is not null;

    partial void OnSelectedHostChanged(HostListItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OpenHostDetailsCommand.NotifyCanExecuteChanged();
    }

    private void OnMetricUpdated(MetricUpdatedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = Hosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyMetric(evt.Metric);
        });
    }

    private void OnStatusChanged(ServerStatusChangedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = Hosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyStatus(evt.Current);
        });
    }

    private void OnHostKeyChanged(HostKeyChangedEvent evt)
    {
        _ui.Post(() =>
        {
            var row = Hosts.FirstOrDefault(host => host.Server.ServerId == evt.ServerId.Value);
            row?.ApplyStatus(ServerStatus.HostKeyRejected);
        });
    }

    private async Task<(IReadOnlyList<float>? Cpu, IReadOnlyList<float>? Memory, IReadOnlyList<float>? Disk, IReadOnlyList<float>? Network)>
        LoadSparklineAsync(Guid serverId, CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddHours(-1);
        var history = await _getHistory.ExecuteAsync(
            new MetricHistoryRequestDto
            {
                ServerId = serverId,
                From = from,
                To = to,
                MaxPoints = HostListItemViewModel.SparklineCapacity,
                Series =
                [
                    MetricSeriesKind.CpuTotal,
                    MetricSeriesKind.MemoryUsed,
                    MetricSeriesKind.DiskUsed,
                    MetricSeriesKind.NetworkReceived,
                ],
            },
            cancellationToken).ConfigureAwait(true);

        return (
            MetricSeriesMapper.ToValues(MetricSeriesMapper.Find(history, MetricSeriesKind.CpuTotal)),
            MetricSeriesMapper.ToValues(MetricSeriesMapper.Find(history, MetricSeriesKind.MemoryUsed)),
            MetricSeriesMapper.ToValues(MetricSeriesMapper.Find(history, MetricSeriesKind.DiskUsed)),
            MetricSeriesMapper.ToValues(MetricSeriesMapper.Find(history, MetricSeriesKind.NetworkReceived)));
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
