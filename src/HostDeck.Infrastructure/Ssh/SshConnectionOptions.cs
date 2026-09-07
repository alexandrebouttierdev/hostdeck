using System;

namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Réglages de transport SSH. Injectés via Options pour rester ajustables sans recompiler.
/// </summary>
public sealed class SshConnectionOptions
{
    public const string SectionName = "Ssh";

    /// <summary>Délai d'établissement de session et d'exécution de commande.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Intervalle keep-alive envoyé au serveur pour détecter une session morte.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Taille maximale de stdout/stderr capturée. Au-delà, la sortie est tronquée et
    /// <see cref="Application.Ports.CommandResult.WasTruncated"/> est vrai (§51, T6).
    /// </summary>
    public int MaxOutputBytes { get; set; } = 1_048_576;
}
