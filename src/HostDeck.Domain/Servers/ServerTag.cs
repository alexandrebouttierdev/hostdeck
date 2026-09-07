using System;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Étiquette libre appliquée à un serveur (« prod », « web », « critique »…).
/// Normalisée en minuscules pour que le filtrage et le regroupement soient prévisibles.
/// </summary>
public sealed record ServerTag
{
    public const int MaxLength = 32;

    public ServerTag(string value)
    {
        var candidate = Guard.RequiredText(value, MaxLength);

        foreach (var character in candidate)
        {
            var isAllowed = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
            if (!isAllowed)
            {
                throw new DomainValidationException(
                    nameof(value),
                    $"le caractère « {character} » n'est pas autorisé dans un tag");
            }
        }

        Value = candidate.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;

    public bool Equals(ServerTag? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}
