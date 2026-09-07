using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Incidents;

namespace HostDeck.Presentation.ViewModels.Incidents;

/// <summary>Incidents actifs + panneau diagnostic. Liste vide → empty state.</summary>
public partial class IncidentsViewModel : PageViewModelBase
{
    private readonly GetIncidentsUseCase _getIncidents;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private IncidentDto? _selectedIncident;

    public ObservableCollection<IncidentDto> Incidents { get; } = [];

    public IncidentsViewModel(GetIncidentsUseCase getIncidents)
    {
        _getIncidents = getIncidents;
    }

    public override string Title => "Incidents actifs";

    public override string Breadcrumb => "Supervision › Incidents actifs";

    public override string StatusSummary =>
        Incidents.Count == 0
            ? "0 incident"
            : $"{Incidents.Count} incident{(Incidents.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedIncident is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var items = await _getIncidents
            .ExecuteAsync(new IncidentFilterDto(), cancellationToken)
            .ConfigureAwait(true);

        Incidents.Clear();
        foreach (var item in items)
        {
            Incidents.Add(item);
        }

        IsEmpty = Incidents.Count == 0;
        if (IsEmpty)
        {
            SelectedIncident = null;
        }

        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSelectedIncidentChanged(IncidentDto? value)
        => OnPropertyChanged(nameof(HasSelection));
}
