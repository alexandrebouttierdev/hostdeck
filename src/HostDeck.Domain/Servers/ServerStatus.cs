namespace HostDeck.Domain.Servers;

/// <summary>
/// État de supervision d'un serveur.
///
/// Les échecs sont volontairement distingués : ils n'appellent ni la même remédiation,
/// ni la même sévérité d'incident. Confondre « injoignable » et « clé d'hôte refusée »
/// ferait passer un événement de sécurité pour une panne réseau (§51, T3).
/// </summary>
public enum ServerStatus
{
    /// <summary>Aucune collecte n'a encore abouti ni échoué.</summary>
    Unknown = 0,

    /// <summary>Dernière collecte réussie.</summary>
    Online = 1,

    /// <summary>L'hôte n'a pas répondu : DNS, réseau ou délai dépassé.</summary>
    Offline = 2,

    /// <summary>
    /// Le bastion est injoignable, donc la cible ne peut pas être atteinte. La cible
    /// elle-même n'est pas déclarée en panne : sa cause racine est ailleurs (§12).
    /// </summary>
    GatewayUnavailable = 3,

    /// <summary>L'hôte a répondu mais a refusé les identifiants.</summary>
    AuthenticationFailed = 4,

    /// <summary>
    /// La clé d'hôte présentée est inconnue ou ne correspond plus à l'empreinte approuvée.
    /// Traité comme un événement de sécurité, jamais contourné automatiquement.
    /// </summary>
    HostKeyRejected = 5,
}
