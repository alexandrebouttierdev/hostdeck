using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HostDeck.Desktop.Bootstrap;
using HostDeck.Presentation.ViewModels.Shell;
using HostDeck.Presentation.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApplication = Avalonia.Application;

namespace HostDeck.Desktop;

/// <summary>
/// Application Avalonia. Elle résout la fenêtre principale via le composition root
/// (<c>HostBuilderFactory</c>) et initialise le shell.
/// </summary>
public partial class App : AvaloniaApplication
{
    private ProcessSignalHandler? _signalHandler;

    /// <summary>Fourni par <c>Program.Main</c> avant le démarrage de la boucle UI.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _signalHandler = ProcessSignalHandler.Attach(desktop);
            desktop.ShutdownRequested += OnShutdownRequested;

            var mainWindow = Services.GetRequiredService<MainWindow>();
            var shell = Services.GetRequiredService<ShellViewModel>();
            mainWindow.DataContext = shell;
            desktop.MainWindow = mainWindow;

            desktop.MainWindow.Opened += async (_, _) =>
            {
                await shell.InitializeAsync().ConfigureAwait(true);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested -= OnShutdownRequested;
        }

        _signalHandler?.Dispose();
        _signalHandler = null;
    }
}
