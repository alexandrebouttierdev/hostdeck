using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Identifiant d'un serveur supervisé. Le type fort évite qu'un identifiant d'incident
/// ou de conteneur soit passé par erreur là où un serveur est attendu (§61).
/// </summary>
public readonly record struct ServerId
{
    public ServerId(Guid value)
    {
        Value = Guard.NotEmpty(value);
    }

    public Guid Value { get; }

    public static ServerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
