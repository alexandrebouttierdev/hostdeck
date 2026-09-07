using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HostDeck.Domain.Shared;

/// <summary>
/// Gardes d'invariants du domaine. Volontairement limité à cette seule responsabilité :
/// ce n'est pas un fourre-tout d'utilitaires (§47).
///
/// Chaque garde lève <see cref="DomainValidationException"/>, de sorte qu'un objet du domaine
/// ne peut jamais exister dans un état invalide.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Exige une chaîne non nulle et non blanche, et renvoie sa version sans espaces de bord.
    /// </summary>
    public static string RequiredText(
        string? value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        var name = parameterName ?? "value";

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(name, "la valeur est obligatoire");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException(
                name,
                $"la valeur dépasse la longueur maximale de {maxLength} caractères");
        }

        return trimmed;
    }

    /// <summary>
    /// Exige une valeur comprise dans un intervalle fermé.
    /// </summary>
    public static T InRange<T>(
        T value,
        T minimum,
        T maximum,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
        {
            throw new DomainValidationException(
                parameterName ?? "value",
                $"la valeur {value} est hors de l'intervalle [{minimum}, {maximum}]");
        }

        return value;
    }

    /// <summary>
    /// Exige une valeur positive ou nulle.
    /// </summary>
    public static T NotNegative<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        where T : INumberBase<T>
    {
        if (T.IsNegative(value))
        {
            throw new DomainValidationException(
                parameterName ?? "value",
                $"la valeur {value} ne peut pas être négative");
        }

        return value;
    }

    /// <summary>
    /// Exige un identifiant non vide.
    /// </summary>
    public static Guid NotEmpty(
        Guid value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException(parameterName ?? "value", "l'identifiant est vide");
        }

        return value;
    }

    /// <summary>
    /// Exige un horodatage réellement daté et exprimé sans ambiguïté de fuseau.
    /// </summary>
    public static DateTimeOffset RequiredInstant(
        DateTimeOffset value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value == default)
        {
            throw new DomainValidationException(parameterName ?? "value", "l'horodatage est obligatoire");
        }

        return value;
    }

    /// <summary>
    /// Exige que <paramref name="later"/> ne précède pas <paramref name="earlier"/>.
    /// </summary>
    public static void NotBefore(
        DateTimeOffset later,
        DateTimeOffset earlier,
        string laterName,
        string earlierName)
    {
        if (later < earlier)
        {
            throw new DomainValidationException(
                laterName,
                $"la date ne peut pas précéder {earlierName}");
        }
    }
}
