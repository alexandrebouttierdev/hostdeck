using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Incidents;

/// <summary>Identifiant d'un incident.</summary>
public readonly record struct IncidentId
{
    public IncidentId(Guid value)
    {
        Value = Guard.NotEmpty(value);
    }

    public Guid Value { get; }

    public static IncidentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
