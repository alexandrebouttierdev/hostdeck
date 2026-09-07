using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Servers;

namespace HostDeck.Presentation.ViewModels.Infrastructure;

/// <summary>
/// Inventaire dense des hôtes + panneau détail à la sélection.
/// Sans serveur : état vide. Sans sélection : panneau non rendu.
/// </summary>
public partial class InfrastructureViewModel : PageViewModelBase
{
    private readonly GetServersUseCase _getServers;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private HostListItemViewModel? _selectedHost;

    public ObservableCollection<HostListItemViewModel> Hosts { get; } = [];

    /// <summary>Branché par le shell pour ouvrir Host Details sans coupler les couches.</summary>
    public Func<ServerSummaryDto, CancellationToken, Task>? OpenHostDetailsHandler { get; set; }

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

    public bool HasSelection => SelectedHost is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _getServers.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        var previousId = SelectedHost?.Server.ServerId;

        Hosts.Clear();
        HostListItemViewModel? restored = null;
        foreach (var server in servers)
        {
            // Séries absentes tant que la collecte / l'historique n'alimentent pas le DTO.
            var row = new HostListItemViewModel(server);
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
}
