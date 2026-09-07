using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Pointeur opaque vers un secret conservé par le trousseau du système d'exploitation.
///
/// C'est le seul lien entre HostDeck et un identifiant SSH : la clé privée, le mot de passe
/// et la passphrase ne traversent jamais le domaine et ne sont jamais persistés en base (§14).
/// Ce type ne contient donc, par construction, aucune donnée sensible.
/// </summary>
public sealed record CredentialReference
{
    public const int MaxLength = 128;

    public CredentialReference(string key, CredentialKind kind)
    {
        Key = Guard.RequiredText(key, MaxLength);
        Kind = kind;
    }

    /// <summary>Clé d'entrée dans le trousseau. Ne contient aucun secret.</summary>
    public string Key { get; }

    /// <summary>Nature du secret référencé, connue sans avoir à le lire.</summary>
    public CredentialKind Kind { get; }

    public static CredentialReference ForServer(ServerId serverId, CredentialKind kind) =>
        new($"hostdeck:server:{serverId.Value:D}:{kind.ToString().ToLowerInvariant()}", kind);

    /// <summary>
    /// N'expose que la clé : ce type peut apparaître dans un log sans risque, et cette
    /// garantie doit rester vraie si de nouveaux membres sont ajoutés (§51, T2).
    /// </summary>
    public override string ToString() => Key;
}

public enum CredentialKind
{
    /// <summary>Clé privée SSH, éventuellement protégée par une passphrase.</summary>
    PrivateKey = 0,

    /// <summary>Mot de passe SSH.</summary>
    Password = 1,
}
