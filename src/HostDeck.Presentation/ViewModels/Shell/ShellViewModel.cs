using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Domain.Servers;
using HostDeck.Presentation.Navigation;
using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Alerts;
using HostDeck.Presentation.ViewModels.Docker;
using HostDeck.Presentation.ViewModels.HostDetails;
using HostDeck.Presentation.ViewModels.Incidents;
using HostDeck.Presentation.ViewModels.Infrastructure;
using HostDeck.Presentation.ViewModels.LiveData;
using HostDeck.Presentation.ViewModels.Overview;
using HostDeck.Presentation.ViewModels.Reports;
using HostDeck.Presentation.ViewModels.Settings;
using HostDeck.Presentation.ViewModels.Topology;

namespace HostDeck.Presentation.ViewModels.Shell;

/// <summary>
/// Coquille : rail, topbar, contenu courant. Aucune logique métier — seulement la navigation
/// et le rafraîchissement des écrans via leurs ViewModels.
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly OverviewViewModel _overview;
    private readonly InfrastructureViewModel _infrastructure;
    private readonly HostDetailsViewModel _hostDetails;
    private readonly IncidentsViewModel _incidents;
    private readonly LiveDataViewModel _liveData;
    private readonly DockerViewModel _docker;
    private readonly AlertsViewModel _alerts;
    private readonly ReportsViewModel _reports;
    private readonly TopologyViewModel _topology;
    private readonly SettingsViewModel _settings;
    private readonly IUiDispatcher _ui;
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    [ObservableProperty]
    private ShellSection _currentSection = ShellSection.Overview;

    [ObservableProperty]
    private PageViewModelBase _currentPage;

    [ObservableProperty]
    private string _collectionStatusText = "Collecte prête";

    [ObservableProperty]
    private bool _hasSecurityAlert;

    [ObservableProperty]
    private string _securityAlertText = string.Empty;

    public ShellViewModel(
        OverviewViewModel overview,
        InfrastructureViewModel infrastructure,
        HostDetailsViewModel hostDetails,
        IncidentsViewModel incidents,
        LiveDataViewModel liveData,
        DockerViewModel docker,
        AlertsViewModel alerts,
        ReportsViewModel reports,
        TopologyViewModel topology,
        SettingsViewModel settings,
        IMonitoringEventBus events,
        IUiDispatcher ui)
    {
        _overview = overview;
        _infrastructure = infrastructure;
        _hostDetails = hostDetails;
        _incidents = incidents;
        _liveData = liveData;
        _docker = docker;
        _alerts = alerts;
        _reports = reports;
        _topology = topology;
        _settings = settings;
        _ui = ui;
        _currentPage = overview;

        _infrastructure.OpenHostDetailsHandler = OpenHostDetailsAsync;

        _subscriptions.Add(events.Subscribe<HostKeyChangedEvent>(OnHostKeyChanged));
        _subscriptions.Add(events.Subscribe<CollectionCycleCompletedEvent>(OnCycleCompleted));
        _subscriptions.Add(events.Subscribe<ServerStatusChangedEvent>(OnStatusChanged));
    }

    public string Breadcrumb => CurrentPage.Breadcrumb;

    public string PageTitle => CurrentPage.Title;

    public string StatusSummary => CurrentPage.StatusSummary;

    public string DatabaseStatusText { get; } = "Base locale prête";

    /// <summary>Charge l'écran initial (flotte vide attendue au premier démarrage).</summary>
    public async Task InitializeAsync(
        ShellSection? initialSection = null,
        CancellationToken cancellationToken = default)
    {
        await NavigateAsync(initialSection ?? ShellSection.Overview, cancellationToken)
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private Task NavigateAsync(ShellSection section, CancellationToken cancellationToken)
        => NavigateCoreAsync(section, selectedHost: null, cancellationToken);

    [RelayCommand]
    private void DismissSecurityAlert()
    {
        HasSecurityAlert = false;
        SecurityAlertText = string.Empty;
    }

    public Task OpenHostDetailsAsync(ServerSummaryDto host, CancellationToken cancellationToken = default)
        => NavigateCoreAsync(ShellSection.HostDetails, host, cancellationToken);

    private async Task NavigateCoreAsync(
        ShellSection section,
        ServerSummaryDto? selectedHost,
        CancellationToken cancellationToken)
    {
        CurrentSection = section;
        CurrentPage = section switch
        {
            ShellSection.Overview => _overview,
            ShellSection.Infrastructure => _infrastructure,
            ShellSection.HostDetails => _hostDetails,
            ShellSection.Incidents => _incidents,
            ShellSection.LiveData => _liveData,
            ShellSection.Docker => _docker,
            ShellSection.Alerts => _alerts,
            ShellSection.Reports => _reports,
            ShellSection.Topology => _topology,
            ShellSection.Settings => _settings,
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
        };

        await RefreshCurrentAsync(selectedHost, cancellationToken).ConfigureAwait(true);

        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(StatusSummary));
    }

    private async Task RefreshCurrentAsync(
        ServerSummaryDto? selectedHost,
        CancellationToken cancellationToken)
    {
        switch (CurrentSection)
        {
            case ShellSection.Overview:
                await _overview.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
            case ShellSection.Infrastructure:
                await _infrastructure.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
            case ShellSection.HostDetails:
                await _hostDetails
                    .ShowHostAsync(selectedHost ?? _infrastructure.SelectedHost?.Server, cancellationToken)
                    .ConfigureAwait(true);
                break;
            case ShellSection.Incidents:
                await _incidents.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
            case ShellSection.Alerts:
                await _alerts.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
            case ShellSection.Settings:
                await _settings.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
            case ShellSection.LiveData:
                await _liveData.RefreshAsync(cancellationToken).ConfigureAwait(true);
                break;
        }
    }

    private void OnHostKeyChanged(HostKeyChangedEvent evt)
    {
        _ui.Post(() =>
        {
            HasSecurityAlert = true;
            SecurityAlertText =
                $"Sécurité : la clé d'hôte de {evt.Host} a changé (serveur {evt.ServerId.Value:D}). "
                + "Vérifiez l'empreinte avant toute approbation.";
        });
    }

    private void OnCycleCompleted(CollectionCycleCompletedEvent evt)
    {
        _ui.Post(() =>
        {
            CollectionStatusText = evt.Failed == 0
                ? $"Collecte OK · {evt.Succeeded} hôte(s)"
                : $"Collecte · {evt.Succeeded} OK · {evt.Failed} échec(s)";
        });
    }

    private void OnStatusChanged(ServerStatusChangedEvent evt)
    {
        if (evt.Current != ServerStatus.HostKeyRejected)
        {
            return;
        }

        _ui.Post(() =>
        {
            HasSecurityAlert = true;
            SecurityAlertText =
                $"Sécurité : clé d'hôte refusée pour le serveur {evt.ServerId.Value:D}.";
        });
    }

    partial void OnCurrentPageChanged(PageViewModelBase value)
    {
        OnPropertyChanged(nameof(Breadcrumb));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(StatusSummary));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        (_overview as IDisposable)?.Dispose();
        (_infrastructure as IDisposable)?.Dispose();
        (_hostDetails as IDisposable)?.Dispose();
        (_liveData as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
