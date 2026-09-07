using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;

namespace HostDeck.Presentation.ViewModels.Infrastructure;

/// <summary>Inventaire dense des hôtes. Sans serveur : état vide explicite.</summary>
public partial class InfrastructureViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private ServerSummaryDto? _selectedHost;

    public ObservableCollection<ServerSummaryDto> Hosts { get; } = [];

    public InfrastructureViewModel(GetServersUseCase getServers)
    {
        _getServers = getServers;
    }

    public override string Title => "Infrastructure";

    public override string Breadcrumb => "Infrastructure › Tous les hôtes";

    public override string StatusSummary =>
        Hosts.Count == 0
            ? "0 hôte"
            : $"{Hosts.Count} hôte{(Hosts.Count > 1 ? "s" : string.Empty)}";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Hosts.Clear();
        foreach (var server in servers)
        {
            Hosts.Add(server);
        }

        IsEmpty = Hosts.Count == 0;
        if (IsEmpty)
        {
            SelectedHost = null;
        }

        OnPropertyChanged(nameof(StatusSummary));
    }
}
