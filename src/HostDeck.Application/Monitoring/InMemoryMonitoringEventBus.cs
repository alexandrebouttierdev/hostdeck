using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using HostDeck.Application.Monitoring.Events;

namespace HostDeck.Application.Monitoring;

/// <summary>
/// Bus d'événements de supervision en mémoire.
///
/// <para>
/// Les publications ne bloquent pas l'appelant : chaque abonné est invoqué sur le thread
/// pool, et une exception d'abonné est avalée pour ne pas interrompre la collecte (§64).
/// L'abonnement renvoie un jeton jetable — s'en défaire suffit à se désabonner.
/// </para>
/// </summary>
public sealed class InMemoryMonitoringEventBus : IMonitoringEventBus
{
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();

    public void Publish(MonitoringEvent monitoringEvent)
    {
        ArgumentNullException.ThrowIfNull(monitoringEvent);

        foreach (var subscriber in _subscribers.Values)
        {
            var captured = subscriber;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    captured.Invoke(monitoringEvent);
                }
#pragma warning disable CA1031 // Intentionnel : un abonné défaillant ne doit pas freiner la collecte (§64).
                catch (Exception)
#pragma warning restore CA1031
                {
                    // Avalée volontairement : la supervision continue.
                }
            });
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : MonitoringEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var id = Guid.NewGuid();
        var subscriber = new Subscriber(typeof(TEvent), evt =>
        {
            if (evt is TEvent typed)
            {
                handler(typed);
            }
        });

        _subscribers[id] = subscriber;
        return new Subscription(this, id);
    }

    private void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);

    private sealed class Subscriber
    {
        private readonly Type _eventType;
        private readonly Action<MonitoringEvent> _handler;

        public Subscriber(Type eventType, Action<MonitoringEvent> handler)
        {
            _eventType = eventType;
            _handler = handler;
        }

        public void Invoke(MonitoringEvent monitoringEvent)
        {
            if (_eventType.IsInstanceOfType(monitoringEvent))
            {
                _handler(monitoringEvent);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private InMemoryMonitoringEventBus? _bus;
        private readonly Guid _id;

        public Subscription(InMemoryMonitoringEventBus bus, Guid id)
        {
            _bus = bus;
            _id = id;
        }

        public void Dispose()
        {
            var bus = Interlocked.Exchange(ref _bus, null);
            bus?.Unsubscribe(_id);
        }
    }
}
