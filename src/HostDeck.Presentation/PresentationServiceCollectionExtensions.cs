using HostDeck.Presentation.Services;
using HostDeck.Presentation.ViewModels.Alerts;
using HostDeck.Presentation.ViewModels.Docker;
using HostDeck.Presentation.ViewModels.HostDetails;
using HostDeck.Presentation.ViewModels.Incidents;
using HostDeck.Presentation.ViewModels.Infrastructure;
using HostDeck.Presentation.ViewModels.LiveData;
using HostDeck.Presentation.ViewModels.Overview;
using HostDeck.Presentation.ViewModels.Reports;
using HostDeck.Presentation.ViewModels.Servers;
using HostDeck.Presentation.ViewModels.Settings;
using HostDeck.Presentation.ViewModels.Shell;
using HostDeck.Presentation.ViewModels.Topology;
using HostDeck.Presentation.Views.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace HostDeck.Presentation;

/// <summary>
/// Enregistre les ViewModels et la fenêtre principale. Le composition root reste Desktop (§15).
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddHostDeckPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();

        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<InfrastructureViewModel>();
        services.AddSingleton<HostDetailsViewModel>();
        services.AddSingleton<IncidentsViewModel>();
        services.AddSingleton<LiveDataViewModel>();
        services.AddSingleton<DockerViewModel>();
        services.AddSingleton<AlertsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<TopologyViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ShellViewModel>();

        services.AddTransient<AddHostViewModel>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
