using System;
using System.Net;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Adresse réseau d'un hôte : nom DNS, adresse IPv4 ou adresse IPv6.
///
/// La validation est stricte parce que cette valeur est transmise à la couche SSH. Elle
/// interdit tout ce qui ressemble à une tentative d'injection (espaces, séparateurs de
/// commande) plutôt que de compter sur un échappement en aval (§51, T4).
/// </summary>
public sealed record HostAddress
{
    public const int MaxLength = 253;

    private HostAddress(string value, HostAddressKind kind)
    {
        Value = value;
        Kind = kind;
    }

    public string Value { get; }

    public HostAddressKind Kind { get; }

    public static HostAddress Parse(string value)
    {
        var candidate = Guard.RequiredText(value, MaxLength);

        if (IPAddress.TryParse(candidate, out var address))
        {
            var kind = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? HostAddressKind.IPv6
                : HostAddressKind.IPv4;

            return new HostAddress(address.ToString(), kind);
        }

        if (!IsValidDnsName(candidate))
        {
            throw new DomainValidationException(
                nameof(value),
                $"« {candidate} » n'est ni une adresse IP ni un nom DNS valide");
        }

        return new HostAddress(candidate, HostAddressKind.DnsName);
    }

    public static bool TryParse(string? value, out HostAddress? address)
    {
        try
        {
            address = Parse(value!);
            return true;
        }
        catch (DomainValidationException)
        {
            address = null;
            return false;
        }
    }

    /// <summary>
    /// Valide un nom DNS selon la RFC 1123 : labels alphanumériques ou tirets, 1 à 63
    /// caractères, ne commençant ni ne finissant par un tiret.
    /// </summary>
    private static bool IsValidDnsName(string candidate)
    {
        // Un point final est licite en DNS absolu, mais inutile ici et source de confusion.
        if (candidate.EndsWith('.') || candidate.StartsWith('.'))
        {
            return false;
        }

        foreach (var label in candidate.Split('.'))
        {
            if (label.Length is 0 or > 63)
            {
                return false;
            }

            if (label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            foreach (var character in label)
            {
                var isAllowed = char.IsAsciiLetterOrDigit(character) || character == '-';
                if (!isAllowed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public override string ToString() => Value;
}

public enum HostAddressKind
{
    DnsName,
    IPv4,
    IPv6,
}
