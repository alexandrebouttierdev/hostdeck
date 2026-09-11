using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;
using HostDeck.Domain.Servers;

namespace HostDeck.Presentation.ViewModels.Topology;

/// <summary>Carte de topologie. Sans hôte : empty state explicite.</summary>
public partial class TopologyViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private ServerSummaryDto? _selectedHost;

    public ObservableCollection<ServerSummaryDto> Hosts { get; } = [];

    public TopologyViewModel(GetServersUseCase getServers)
    {
        _getServers = getServers;
    }

    public override string Title => "Topologie";

    public override string Breadcrumb => "Infrastructure › Topologie";

    public override string StatusSummary =>
        IsEmpty
            ? "Carte vide"
            : $"{Hosts.Count} nœud{(Hosts.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedHost is not null;

    public int JumpHostCount => Hosts.Count(host => host.ConnectionMode == ConnectionMode.JumpHost);

    public int DirectHostCount => Hosts.Count - JumpHostCount;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var previousId = SelectedHost?.ServerId;

        Hosts.Clear();
        foreach (var server in servers)
        {
            Hosts.Add(server);
        }

        IsEmpty = Hosts.Count == 0;
        SelectedHost = previousId is Guid id
            ? Hosts.FirstOrDefault(host => host.ServerId == id)
            : null;

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(JumpHostCount));
        OnPropertyChanged(nameof(DirectHostCount));
    }

    partial void OnSelectedHostChanged(ServerSummaryDto? value)
        => OnPropertyChanged(nameof(HasSelection));
}
