using System;

namespace HostDeck.Application.Dtos.Servers;

/// <summary>
/// Décision explicite d'approuver une empreinte de clé d'hôte (§51, T3).
/// Jamais appelée automatiquement : l'opérateur doit confirmer.
/// </summary>
public sealed record ApproveHostKeyDto
{
    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    /// <summary>Empreinte SHA256 présentée par l'hôte.</summary>
    public required string Fingerprint { get; init; }
}
