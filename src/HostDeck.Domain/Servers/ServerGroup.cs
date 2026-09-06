using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Groupe auquel appartient un serveur (« Databases », « Frontend »…).
/// Sert de périmètre aux règles d'alerte (§21) et d'axe de regroupement dans l'inventaire.
/// </summary>
public sealed record ServerGroup
{
    public const int MaxLength = 64;

    public ServerGroup(string name)
    {
        Name = Guard.RequiredText(name, MaxLength);
    }

    public string Name { get; }

    public override string ToString() => Name;

    public bool Equals(ServerGroup? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
