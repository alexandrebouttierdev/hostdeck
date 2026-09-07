using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace HostDeck.Desktop.Bootstrap;

/// <summary>
/// Traduit les signaux d'arrêt du système (SIGTERM, SIGINT, SIGQUIT) en une fermeture ordonnée
/// de l'application Avalonia.
///
/// Sans cela, un `kill` ou un arrêt de session laisse le processus vivant : la boucle Avalonia
/// ignore le signal et les services de fond ne sont jamais annulés (§61, arrêt propre).
/// </summary>
internal sealed class ProcessSignalHandler : IDisposable
{
    private static readonly PosixSignal[] ShutdownSignals =
    [
        PosixSignal.SIGTERM,
        PosixSignal.SIGINT,
        PosixSignal.SIGQUIT,
    ];

    private readonly List<PosixSignalRegistration> _registrations = [];
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private bool _disposed;

    private ProcessSignalHandler(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public static ProcessSignalHandler Attach(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        var handler = new ProcessSignalHandler(lifetime);
        foreach (var signal in ShutdownSignals)
        {
            handler._registrations.Add(PosixSignalRegistration.Create(signal, handler.OnSignal));
        }

        return handler;
    }

    private void OnSignal(PosixSignalContext context)
    {
        // On prend la main sur le signal : le runtime ne doit pas tuer le processus avant
        // que la fenêtre et les services de fond aient été arrêtés.
        context.Cancel = true;

        // Shutdown touche à l'arbre visuel et doit donc s'exécuter sur le thread UI (§36).
        Dispatcher.UIThread.Post(() => _lifetime.Shutdown());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }
}
