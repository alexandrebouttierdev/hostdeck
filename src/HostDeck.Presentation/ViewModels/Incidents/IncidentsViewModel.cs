using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Incidents;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Domain.Incidents;
using HostDeck.Presentation.Services;

namespace HostDeck.Presentation.ViewModels.Incidents;

/// <summary>Incidents actifs + panneau diagnostic. Liste vide → empty state.</summary>
public partial class IncidentsViewModel : PageViewModelBase, IDisposable
{
    private static readonly IReadOnlyList<IncidentStatus> ActiveStatuses =
    [
        IncidentStatus.Open,
        IncidentStatus.Acknowledged,
        IncidentStatus.Recovered,
    ];

    private readonly GetIncidentsUseCase _getIncidents;
    private readonly GetIncidentDetailsUseCase _getDetails;
    private readonly AcknowledgeIncidentUseCase _acknowledge;
    private readonly ResolveIncidentUseCase _resolve;
    private readonly IMonitoringEventBus _events;
    private readonly IUiDispatcher _ui;
    private readonly IDisposable _subscription;
    private bool _disposed;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private IncidentDto? _selectedIncident;

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private int _highCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private string? _selectedRuleName;

    [ObservableProperty]
    private string? _selectedRuleDescription;

    public ObservableCollection<IncidentDto> Incidents { get; } = [];

    public ObservableCollection<IncidentTimelineItemViewModel> Timeline { get; } = [];

    public IncidentsViewModel(
        GetIncidentsUseCase getIncidents,
        GetIncidentDetailsUseCase getDetails,
        AcknowledgeIncidentUseCase acknowledge,
        ResolveIncidentUseCase resolve,
        IMonitoringEventBus events,
        IUiDispatcher ui)
    {
        _getIncidents = getIncidents;
        _getDetails = getDetails;
        _acknowledge = acknowledge;
        _resolve = resolve;
        _events = events;
        _ui = ui;
        _subscription = _events.Subscribe<IncidentChangedEvent>(OnIncidentChanged);
    }

    public override string Title => "Incidents actifs";

    public override string Breadcrumb => "Supervision › Incidents actifs";

    public override string StatusSummary =>
        Incidents.Count == 0
            ? "0 incident"
            : $"{Incidents.Count} incident{(Incidents.Count > 1 ? "s" : string.Empty)}";

    public bool HasSelection => SelectedIncident is not null;

    public bool CanAcknowledge => SelectedIncident?.Status == IncidentStatus.Open;

    public bool CanResolve => SelectedIncident?.Status is IncidentStatus.Open or IncidentStatus.Acknowledged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var items = await _getIncidents
            .ExecuteAsync(new IncidentFilterDto { Statuses = ActiveStatuses }, cancellationToken)
            .ConfigureAwait(true);

        var selectedId = SelectedIncident?.IncidentId;
        Incidents.Clear();
        foreach (var item in items)
        {
            Incidents.Add(item);
        }

        UpdateSeverityCounters();
        IsEmpty = Incidents.Count == 0;

        if (IsEmpty)
        {
            SelectedIncident = null;
            ClearDetails();
        }
        else if (selectedId is Guid id)
        {
            SelectedIncident = Incidents.FirstOrDefault(incident => incident.IncidentId == id);
        }

        if (SelectedIncident is not null)
        {
            await LoadDetailsAsync(SelectedIncident.IncidentId, cancellationToken).ConfigureAwait(true);
        }

        NotifyCommandStates();
        OnPropertyChanged(nameof(StatusSummary));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand(CanExecute = nameof(CanAcknowledge))]
    private async Task AcknowledgeAsync(CancellationToken cancellationToken)
    {
        if (SelectedIncident is null)
        {
            return;
        }

        await _acknowledge.ExecuteAsync(
            new AcknowledgeIncidentDto
            {
                IncidentId = SelectedIncident.IncidentId,
                AcknowledgedBy = "local",
            },
            cancellationToken).ConfigureAwait(true);

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanResolve))]
    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (SelectedIncident is null)
        {
            return;
        }

        await _resolve.ExecuteAsync(SelectedIncident.IncidentId, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    partial void OnSelectedIncidentChanged(IncidentDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        NotifyCommandStates();

        if (value is null)
        {
            ClearDetails();
            return;
        }

        _ = LoadDetailsAsync(value.IncidentId);
    }

    private async Task LoadDetailsAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var details = await _getDetails.ExecuteAsync(incidentId, cancellationToken).ConfigureAwait(true);

        _ui.Post(() =>
        {
            if (SelectedIncident?.IncidentId != incidentId)
            {
                return;
            }

            SelectedRuleName = details.RuleName;
            SelectedRuleDescription = details.RuleDescription;
            Timeline.Clear();
            foreach (var eventDto in details.Timeline)
            {
                Timeline.Add(new IncidentTimelineItemViewModel(eventDto));
            }

            NotifyCommandStates();
        });
    }

    private void ClearDetails()
    {
        Timeline.Clear();
        SelectedRuleName = null;
        SelectedRuleDescription = null;
    }

    private void UpdateSeverityCounters()
    {
        CriticalCount = Incidents.Count(incident => incident.Severity == Severity.Critical);
        HighCount = Incidents.Count(incident => incident.Severity == Severity.High);
        WarningCount = Incidents.Count(incident => incident.Severity == Severity.Warning);
    }

    private void OnIncidentChanged(IncidentChangedEvent evt)
    {
        _ui.Post(() => _ = RefreshAsync());
    }

    private void NotifyCommandStates()
    {
        OnPropertyChanged(nameof(CanAcknowledge));
        OnPropertyChanged(nameof(CanResolve));
        AcknowledgeCommand.NotifyCanExecuteChanged();
        ResolveCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscription.Dispose();
        GC.SuppressFinalize(this);
    }
}
