using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Servers;

namespace HostDeck.Presentation.ViewModels.HostDetails;

/// <summary>
/// Détail d'un hôte. Sans sélection : panneau vide. Les graphiques hors historique réel
/// restent en placeholder.
/// </summary>
public partial class HostDetailsViewModel : PageViewModelBase
{
    [ObservableProperty]
    private ServerSummaryDto? _selectedHost;

    [ObservableProperty]
    private bool _hasSelection;

    public override string Title => "Détail de l’hôte";

    public override string Breadcrumb =>
        HasSelection && SelectedHost is not null
            ? $"Infrastructure › {SelectedHost.Name}"
            : "Infrastructure › Détail de l’hôte";

    public override string StatusSummary =>
        HasSelection && SelectedHost is not null ? SelectedHost.Name : "Aucun hôte sélectionné";

    public void ShowHost(ServerSummaryDto? host)
    {
        SelectedHost = host;
        HasSelection = host is not null;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(StatusSummary));
    }

    partial void OnSelectedHostChanged(ServerSummaryDto? value)
    {
        HasSelection = value is not null;
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(StatusSummary));
    }
}
