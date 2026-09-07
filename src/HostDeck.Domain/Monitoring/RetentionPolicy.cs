using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Durée de conservation des données locales, par nature de données.
///
/// Les métriques et les événements ont des durées distinctes parce qu'ils n'ont ni le même
/// volume ni la même valeur dans le temps : on garde un incident bien plus longtemps qu'un
/// point de mesure à la seconde.
/// </summary>
public sealed record RetentionPolicy
{
    public static readonly TimeSpan MinimumRetention = TimeSpan.FromDays(1);
    public static readonly TimeSpan MaximumRetention = TimeSpan.FromDays(3650);

    /// <summary>Valeurs par défaut des maquettes : 90 jours de métriques, 180 d'événements.</summary>
    public static readonly RetentionPolicy Default = new(TimeSpan.FromDays(90), TimeSpan.FromDays(180));

    public RetentionPolicy(TimeSpan metricRetention, TimeSpan eventRetention)
    {
        MetricRetention = Guard.InRange(metricRetention, MinimumRetention, MaximumRetention);
        EventRetention = Guard.InRange(eventRetention, MinimumRetention, MaximumRetention);
    }

    public TimeSpan MetricRetention { get; }

    public TimeSpan EventRetention { get; }

    /// <summary>
    /// Instant avant lequel les métriques peuvent être purgées.
    /// </summary>
    public DateTimeOffset MetricCutoff(DateTimeOffset now) => now.ToUniversalTime() - MetricRetention;

    /// <summary>
    /// Instant avant lequel les événements peuvent être purgés.
    /// </summary>
    public DateTimeOffset EventCutoff(DateTimeOffset now) => now.ToUniversalTime() - EventRetention;

    public static RetentionPolicy FromDays(int metricDays, int eventDays) =>
        new(TimeSpan.FromDays(metricDays), TimeSpan.FromDays(eventDays));
}
