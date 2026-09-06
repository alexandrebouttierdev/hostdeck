using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HostDeck.Desktop.Bootstrap;
using HostDeck.Presentation.Views.Shell;
// Le namespace HostDeck.Application masque le type Avalonia.Application depuis
// l'intérieur de HostDeck.* : l'alias désigne sans ambiguïté la classe Avalonia.
using AvaloniaApplication = Avalonia.Application;

namespace HostDeck.Desktop;

/// <summary>
/// Application Avalonia. Elle ne fait que résoudre la fenêtre principale : la construction
/// du graphe de dépendances appartient au composition root (<c>HostBuilderFactory</c>).
/// </summary>
public partial class App : AvaloniaApplication
{
    private ProcessSignalHandler? _signalHandler;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _signalHandler = ProcessSignalHandler.Attach(desktop);
            desktop.ShutdownRequested += OnShutdownRequested;
            desktop.MainWindow = new MainWindow();
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
