namespace HostDeck.Domain.Servers;

/// <summary>
/// Chemin emprunté pour joindre un serveur. Déduit de la configuration, jamais saisi :
/// un serveur possède un bastion ou n'en possède pas.
/// </summary>
public enum ConnectionMode
{
    /// <summary>Connexion SSH directe vers l'hôte.</summary>
    Direct = 0,

    /// <summary>Connexion SSH traversant un bastion.</summary>
    JumpHost = 1,
}
