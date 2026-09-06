using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Trafic d'une interface réseau.
///
/// <c>/proc/net/dev</c> ne fournit que des compteurs cumulés. Le débit est donc toujours
/// dérivé de deux relevés successifs, jamais lu directement : d'où la présence des compteurs
/// bruts et la méthode <see cref="RateSince"/> qui en fait un débit.
/// </summary>
public sealed record NetworkUsage
{
    public const int MaxInterfaceNameLength = 64;

    public NetworkUsage(string interfaceName, ByteSize received, ByteSize transmitted)
    {
        InterfaceName = Guard.RequiredText(interfaceName, MaxInterfaceNameLength);
        Received = received;
        Transmitted = transmitted;
    }

    public string InterfaceName { get; }

    /// <summary>Total cumulé d'octets reçus depuis le démarrage de l'interface.</summary>
    public ByteSize Received { get; }

    /// <summary>Total cumulé d'octets émis depuis le démarrage de l'interface.</summary>
    public ByteSize Transmitted { get; }

    /// <summary>
    /// Calcule le débit entre ce relevé et un relevé antérieur de la même interface.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// Si les deux relevés ne portent pas sur la même interface : comparer deux interfaces
    /// différentes produirait un débit silencieusement faux.
    /// </exception>
    public NetworkRate RateSince(NetworkUsage previous, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(previous);

        if (!string.Equals(previous.InterfaceName, InterfaceName, StringComparison.Ordinal))
        {
            throw new DomainValidationException(
                nameof(previous),
                $"le relevé précédent porte sur l'interface « {previous.InterfaceName} », pas « {InterfaceName} »");
        }

        if (elapsed <= TimeSpan.Zero)
        {
            return NetworkRate.Zero(InterfaceName);
        }

        var seconds = elapsed.TotalSeconds;

        // La soustraction de ByteSize est bornée à zéro : un compteur remis à zéro par un
        // redémarrage d'interface donne 0 plutôt qu'un débit négatif.
        return new NetworkRate(
            InterfaceName,
            (Received - previous.Received).Bytes / seconds,
            (Transmitted - previous.Transmitted).Bytes / seconds);
    }
}

/// <summary>
/// Débit instantané d'une interface, en octets par seconde.
/// </summary>
public readonly record struct NetworkRate
{
    public NetworkRate(string interfaceName, double receivedBytesPerSecond, double transmittedBytesPerSecond)
    {
        InterfaceName = Guard.RequiredText(interfaceName, NetworkUsage.MaxInterfaceNameLength);
        ReceivedBytesPerSecond = Guard.NotNegative(receivedBytesPerSecond);
        TransmittedBytesPerSecond = Guard.NotNegative(transmittedBytesPerSecond);
    }

    public string InterfaceName { get; }

    public double ReceivedBytesPerSecond { get; }

    public double TransmittedBytesPerSecond { get; }

    public static NetworkRate Zero(string interfaceName) => new(interfaceName, 0d, 0d);
}
