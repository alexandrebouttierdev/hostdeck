using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Période entre deux collectes pour un serveur.
///
/// La borne basse n'est pas cosmétique : un intervalle trop court transforme HostDeck en
/// source de charge sur la production qu'il est censé surveiller (§51, T8).
/// </summary>
public readonly record struct MonitoringInterval
{
    public static readonly TimeSpan MinimumValue = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaximumValue = TimeSpan.FromHours(1);

    /// <summary>Intervalle par défaut, aligné sur celui des maquettes (60 s).</summary>
    public static readonly MonitoringInterval Default = new(TimeSpan.FromSeconds(60));

    public MonitoringInterval(TimeSpan value)
    {
        Value = Guard.InRange(value, MinimumValue, MaximumValue);
    }

    public TimeSpan Value { get; }

    public static MonitoringInterval FromSeconds(int seconds) =>
        new(TimeSpan.FromSeconds(Guard.InRange(
            seconds,
            (int)MinimumValue.TotalSeconds,
            (int)MaximumValue.TotalSeconds)));

    public override string ToString() => Value.ToString();
}
