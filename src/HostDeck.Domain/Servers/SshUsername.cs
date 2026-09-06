using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Nom d'utilisateur SSH.
///
/// Comme <see cref="HostAddress"/>, cette valeur atteint la couche SSH : le jeu de caractères
/// est restreint à celui d'un nom d'utilisateur POSIX pour qu'aucune saisie ne puisse être
/// interprétée comme un fragment de commande (§51, T4).
/// </summary>
public sealed record SshUsername
{
    public const int MaxLength = 32;

    public SshUsername(string value)
    {
        var candidate = Guard.RequiredText(value, MaxLength);

        foreach (var character in candidate)
        {
            var isAllowed = char.IsAsciiLetterOrDigit(character)
                || character is '_' or '-' or '.' or '$';

            if (!isAllowed)
            {
                throw new DomainValidationException(
                    nameof(value),
                    $"le caractère « {character} » n'est pas autorisé dans un nom d'utilisateur SSH");
            }
        }

        if (candidate[0] is '-')
        {
            throw new DomainValidationException(
                nameof(value),
                "un nom d'utilisateur ne peut pas commencer par un tiret");
        }

        Value = candidate;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
