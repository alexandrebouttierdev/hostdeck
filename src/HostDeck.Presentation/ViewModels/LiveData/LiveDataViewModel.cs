using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Servers;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.ViewModels.Infrastructure;

namespace HostDeck.Presentation.ViewModels.LiveData;

/// <summary>Métriques temps réel. Sans flotte / sans sélection : empty state + cadres vides.</summary>
public partial class LiveDataViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private HostListItemViewModel? _selectedHost;

    public ObservableCollection<HostListItemViewModel> Hosts { get; } = [];

    public IReadOnlyList<ChartSeriesData>? CpuSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? MemorySeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? DiskSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? NetworkSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? LoadSeries { get; private set; }

    public LiveDataViewModel(GetServersUseCase getServers)
    {
        _getServers = getServers;
    }

    public override string Title => "Données en direct";

    public override string Breadcrumb => "Supervision › Données en direct";

    public override string StatusSummary =>
        SelectedHost is null ? "Aucune série" : SelectedHost.Name;

    public bool HasSelection => SelectedHost is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Hosts.Clear();
        foreach (var server in servers)
        {
            Hosts.Add(new HostListItemViewModel(server));
        }

        IsEmpty = Hosts.Count == 0;
        if (IsEmpty)
        {
            SelectedHost = null;
        }

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CpuSeries));
        OnPropertyChanged(nameof(MemorySeries));
        OnPropertyChanged(nameof(DiskSeries));
        OnPropertyChanged(nameof(NetworkSeries));
        OnPropertyChanged(nameof(LoadSeries));
    }

    partial void OnSelectedHostChanged(HostListItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(StatusSummary));
    }
}
