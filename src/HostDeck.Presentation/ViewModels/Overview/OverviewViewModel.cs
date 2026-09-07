using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Servers;

namespace HostDeck.Presentation.ViewModels.Overview;

/// <summary>Vue d'ensemble : KPIs et panneaux. Flotte vide → états vides, jamais de séries inventées.</summary>
public partial class OverviewViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private int _hostCount;

    [ObservableProperty]
    private int _activeIncidentCount;

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
        // Les incidents actifs seront branchés quand le coordinateur existera ; V1 n'invente rien.
        ActiveIncidentCount = 0;
        OnPropertyChanged(nameof(StatusSummary));
    }
}
