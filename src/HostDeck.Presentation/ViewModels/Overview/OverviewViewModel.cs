using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Servers;
using HostDeck.Presentation.Controls.Charts;
using HostDeck.Presentation.ViewModels.Infrastructure;

namespace HostDeck.Presentation.ViewModels.Overview;

/// <summary>
/// Vue d'ensemble : KPIs, cadres graphiques et top hôtes.
/// Flotte vide → états vides, jamais de séries inventées.
/// </summary>
public partial class OverviewViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _hostCount;

    [ObservableProperty]
    private int _activeIncidentCount;

    public ObservableCollection<HostListItemViewModel> TopHosts { get; } = [];

    /// <summary>Séries globales — null tant qu'aucune collecte n'a fourni d'historique.</summary>
    public IReadOnlyList<ChartSeriesData>? CpuSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? MemorySeries { get; private set; }

    public OverviewViewModel(GetServersUseCase getServers)
    {
        _getServers = getServers;
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
        ActiveIncidentCount = 0;

        TopHosts.Clear();
        foreach (var server in servers)
        {
            TopHosts.Add(new HostListItemViewModel(server));
        }

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(CpuSeries));
        OnPropertyChanged(nameof(MemorySeries));
    }
}
