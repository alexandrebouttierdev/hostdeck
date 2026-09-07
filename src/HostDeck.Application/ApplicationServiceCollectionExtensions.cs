using FluentValidation;
using HostDeck.Application.Alerts;
using HostDeck.Application.Incidents;
using HostDeck.Application.Monitoring;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Application.Servers;
using HostDeck.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace HostDeck.Application;

/// <summary>
/// Enregistre les use cases et les validateurs de la couche Application.
///
/// La couche décrit elle-même ce qu'elle expose ; le composition root se contente de
/// l'appeler, sans avoir à énumérer chaque type (§6).
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddHostDeckApplication(this IServiceCollection services)
    {
        // Les validateurs sont sans état et réutilisables : une seule instance suffit.
        services.AddValidatorsFromAssemblyContaining<CreateServerDtoValidatorMarker>(
            ServiceLifetime.Singleton);

        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton<IMonitoringEventBus, InMemoryMonitoringEventBus>();

        // Les use cases ne portent aucun état entre deux appels ; ils sont créés à la demande
        // et ne retiennent donc jamais un dépôt au-delà de l'opération en cours.
        services.AddTransient<AddServerUseCase>();
        services.AddTransient<UpdateServerUseCase>();
        services.AddTransient<DeleteServerUseCase>();
        services.AddTransient<GetServersUseCase>();
        services.AddTransient<GetServerDetailsUseCase>();
        services.AddTransient<TestConnectionUseCase>();
        services.AddTransient<ApproveHostKeyUseCase>();
        services.AddTransient<GetMetricHistoryUseCase>();

        services.AddTransient<GetIncidentsUseCase>();
        services.AddTransient<GetIncidentDetailsUseCase>();
        services.AddTransient<AcknowledgeIncidentUseCase>();
        services.AddTransient<ResolveIncidentUseCase>();

        services.AddTransient<GetAlertRulesUseCase>();
        services.AddTransient<CreateAlertRuleUseCase>();
        services.AddTransient<UpdateAlertRuleUseCase>();
        services.AddTransient<DeleteAlertRuleUseCase>();

        services.AddTransient<GetSettingsUseCase>();
        services.AddTransient<UpdateSettingsUseCase>();

        return services;
    }
}

/// <summary>
/// Ancre de découverte des validateurs. Un type dédié plutôt qu'un validateur arbitraire :
/// renommer ou déplacer un validateur ne doit pas casser silencieusement l'enregistrement.
/// </summary>
public sealed class CreateServerDtoValidatorMarker;
