using System;

namespace HostDeck.Application.Monitoring.Events;

/// <summary>
/// Bus d'événements de supervision.
///
/// Volontairement minimal et non statique : un bus global statique retiendrait les abonnés
/// pour la durée du processus, ce qui est exactement la fuite que le §64 interdit.
/// L'abonnement renvoie un jeton jetable — s'en défaire suffit à se désabonner.
/// </summary>
public interface IMonitoringEventBus
{
    /// <summary>
    /// Publie un événement. Ne bloque jamais l'appelant : la collecte ne doit pas ralentir
    /// parce qu'un abonné est lent.
    /// </summary>
    void Publish(MonitoringEvent monitoringEvent);

    /// <summary>
    /// S'abonne à un type d'événement. Libérer le jeton renvoyé annule l'abonnement.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : MonitoringEvent;
}
