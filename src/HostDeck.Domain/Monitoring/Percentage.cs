using System.Globalization;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Monitoring;

/// <summary>
/// Pourcentage borné à [0, 100].
///
/// Le bornage est délibéré : un calcul de CPU à partir de deux relevés de <c>/proc/stat</c>
/// peut produire 100,3 % par arrondi ou dérive d'horloge, et une valeur hors bornes se
/// propagerait ensuite dans les seuils d'alerte et l'échelle des graphiques.
/// </summary>
public readonly record struct Percentage
{
    public const double Minimum = 0d;
    public const double Maximum = 100d;

    public static readonly Percentage Zero = new(0d);

    public Percentage(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new DomainValidationException(nameof(value), "le pourcentage n'est pas un nombre fini");
        }

        Value = Guard.InRange(value, Minimum, Maximum);
    }

    public double Value { get; }

    /// <summary>
    /// Construit un pourcentage en ramenant dans les bornes une valeur calculée légèrement
    /// hors intervalle. Réservé aux parsers, qui absorbent le bruit de mesure ; tout autre
    /// appelant doit utiliser le constructeur et échouer sur une valeur aberrante.
    /// </summary>
    public static Percentage Clamp(double value)
    {
        if (double.IsNaN(value))
        {
            throw new DomainValidationException(nameof(value), "le pourcentage n'est pas un nombre");
        }

        return new Percentage(double.Clamp(value, Minimum, Maximum));
    }

    /// <summary>
    /// Calcule un pourcentage d'utilisation. Une capacité nulle donne 0 % plutôt qu'une
    /// division par zéro : un système de fichiers de taille nulle n'est pas saturé.
    /// </summary>
    public static Percentage OfRatio(double used, double total) =>
        total <= 0d ? Zero : Clamp(used / total * 100d);

    public override string ToString() =>
        Value.ToString("0.#", CultureInfo.InvariantCulture) + " %";
}
