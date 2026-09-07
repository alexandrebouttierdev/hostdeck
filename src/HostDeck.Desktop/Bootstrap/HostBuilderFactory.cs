using HostDeck.Application;
using HostDeck.Infrastructure;
using HostDeck.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HostDeck.Desktop.Bootstrap;

/// <summary>
/// Composition root de HostDeck (§6). C'est le seul endroit qui connaît à la fois
/// l'Infrastructure et la Presentation : il assemble le graphe de dépendances,
/// configure le logging et pilote le cycle de vie des services de fond.
/// </summary>
internal static class HostBuilderFactory
{
    public static IHost Create(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        builder.Services.AddHostDeckApplication();
        builder.Services.AddHostDeckInfrastructure();
        builder.Services.AddHostDeckPresentation();

        return builder.Build();
    }
}
