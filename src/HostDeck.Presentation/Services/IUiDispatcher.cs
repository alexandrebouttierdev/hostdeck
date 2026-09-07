using System;
using System.Threading.Tasks;

namespace HostDeck.Presentation.Services;

/// <summary>
/// Marshalage vers le thread UI Avalonia. Les abonnements au bus arrivent hors UI (§20).
/// </summary>
public interface IUiDispatcher
{
    void Post(Action action);

    Task InvokeAsync(Action action);
}
