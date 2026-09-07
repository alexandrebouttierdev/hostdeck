using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Alerts;

/// <summary>
/// Périmètre d'application d'une règle : globale, un groupe, ou un serveur précis (§21).
///
/// La spécificité sert à l'arbitrage : quand plusieurs règles visent la même métrique sur un
/// même hôte, la plus spécifique l'emporte, faute de quoi une règle globale écraserait une
/// exception posée volontairement sur un serveur.
/// </summary>
public sealed record AlertScope
{
    public static readonly AlertScope Global = new(AlertScopeKind.Global, null, null);

    private AlertScope(AlertScopeKind kind, ServerGroup? group, ServerId? serverId)
    {
        Kind = kind;
        Group = group;
        ServerId = serverId;
    }

    public AlertScopeKind Kind { get; }

    public ServerGroup? Group { get; }

    public ServerId? ServerId { get; }

    /// <summary>
    /// Spécificité croissante. Comparable directement : une valeur plus haute l'emporte.
    /// </summary>
    public int Specificity => Kind switch
    {
        AlertScopeKind.Server => 2,
        AlertScopeKind.Group => 1,
        _ => 0,
    };

    public static AlertScope ForGroup(ServerGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new AlertScope(AlertScopeKind.Group, group, null);
    }

    public static AlertScope ForServer(ServerId serverId) =>
        new(AlertScopeKind.Server, null, serverId);

    /// <summary>
    /// Détermine si ce périmètre couvre un serveur donné.
    /// </summary>
    public bool Covers(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return Kind switch
        {
            AlertScopeKind.Global => true,
            AlertScopeKind.Group => Group is not null && Group == server.Group,
            AlertScopeKind.Server => ServerId == server.Id,
            _ => false,
        };
    }

    public override string ToString() => Kind switch
    {
        AlertScopeKind.Group => $"groupe {Group}",
        AlertScopeKind.Server => $"serveur {ServerId}",
        _ => "tous les serveurs",
    };
}

public enum AlertScopeKind
{
    Global = 0,
    Group = 1,
    Server = 2,
}
