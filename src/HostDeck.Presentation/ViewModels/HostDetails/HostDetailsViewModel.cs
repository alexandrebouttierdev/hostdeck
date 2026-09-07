using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Presentation.Controls.Charts;

namespace HostDeck.Presentation.ViewModels.HostDetails;

/// <summary>
/// Détail d'un hôte. Sans sélection : panneau vide. Les graphiques hors historique réel
/// restent en cadre + empty state.
/// </summary>
public partial class HostDetailsViewModel : PageViewModelBase
{
    [ObservableProperty]
    private ServerSummaryDto? _selectedHost;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>Séries host — absentes tant que l'historique n'est pas branché.</summary>
    public IReadOnlyList<ChartSeriesData>? CpuSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? MemorySeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? DiskSeries { get; private set; }

    public IReadOnlyList<ChartSeriesData>? NetworkSeries { get; private set; }

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
        OnPropertyChanged(nameof(CpuSeries));
        OnPropertyChanged(nameof(MemorySeries));
        OnPropertyChanged(nameof(DiskSeries));
        OnPropertyChanged(nameof(NetworkSeries));
    }

    partial void OnSelectedHostChanged(ServerSummaryDto? value)
    {
        HasSelection = value is not null;
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(StatusSummary));
    }
}
