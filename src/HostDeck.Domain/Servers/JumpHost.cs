using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Bastion traversé pour atteindre une cible.
///
/// Le bastion porte sa propre référence de credential : il s'authentifie séparément de la
/// cible. Comme partout ailleurs, seule une référence est conservée, jamais le secret (§14).
/// </summary>
public sealed record JumpHost
{
    public JumpHost(
        HostAddress address,
        Port port,
        SshUsername username,
        CredentialReference credential)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(credential);

        Address = address;
        Port = port;
        Username = username;
        Credential = credential;
    }

    public HostAddress Address { get; }

    public Port Port { get; }

    public SshUsername Username { get; }

    public CredentialReference Credential { get; }

    public override string ToString() => $"{Username}@{Address}:{Port}";
}
