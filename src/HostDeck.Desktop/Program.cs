using System;
using Avalonia;
using HostDeck.Desktop.Bootstrap;
using HostDeck.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HostDeck.Desktop;

internal static class Program
{
    /// <summary>
    /// Délai maximal laissé aux services de fond pour s'arrêter proprement avant abandon.
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Point d'entrée. Le hôte générique est démarré avant Avalonia et arrêté après la fermeture
    /// de la fenêtre principale, ce qui garantit l'annulation des services de fond (§17, §61).
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        using var host = HostBuilderFactory.Create(args);

        host.Start();
        try
        {
            // Prépare SQLite avant d'ouvrir l'UI : les écrans lisent l'inventaire et les réglages.
            host.Services.InitializeHostDeckDatabaseAsync().GetAwaiter().GetResult();

            App.Services = host.Services;
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Seule attente synchrone tolérée du projet : le point d'entrée est synchrone
            // (contrainte [STAThread]) et la boucle Avalonia est déjà terminée ici, donc
            // aucun contexte de synchronisation ne peut provoquer d'interblocage (§47).
            host.StopAsync(ShutdownTimeout).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Utilisé par le point d'entrée et par l'outillage de conception Avalonia.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
