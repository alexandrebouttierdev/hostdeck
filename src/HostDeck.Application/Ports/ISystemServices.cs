using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Domain.Incidents;

namespace HostDeck.Application.Ports;

/// <summary>
/// Source de temps.
///
/// Injectée plutôt que d'appeler <c>DateTimeOffset.UtcNow</c> directement : les durées,
/// cooldowns et fenêtres de confirmation ne seraient pas testables autrement qu'en faisant
/// dormir les tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Horloge réelle du système.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Notification bureau.
///
/// Volontairement non bloquante pour la supervision : perdre une notification ne doit jamais
/// interrompre une collecte, donc les implémentations journalisent l'échec au lieu de le
/// propager.
/// </summary>
public interface IDesktopNotificationService
{
    Task NotifyIncidentAsync(
        IncidentNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vrai si le système accepte les notifications. Faux sur un poste où l'utilisateur les
    /// a désactivées, ce que l'écran Paramètres doit pouvoir indiquer.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contenu d'une notification.
///
/// Ne porte que des libellés déjà destinés à l'affichage : aucun champ ne peut contenir de
/// secret, puisque les notifications sortent du processus (§64).
/// </summary>
public sealed record IncidentNotification
{
    public required Guid IncidentId { get; init; }

    public required Severity Severity { get; init; }

    public required string ServerName { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }
}
