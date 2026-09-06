using System.Globalization;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Alerts;

/// <summary>
/// Comparaison qui déclenche une règle d'alerte.
/// </summary>
public enum ComparisonOperator
{
    GreaterThan = 0,
    GreaterThanOrEqual = 1,
    LessThan = 2,
    LessThanOrEqual = 3,
}

/// <summary>
/// Condition numérique d'une règle : un opérateur et une valeur.
///
/// La validation dépend de la métrique : un seuil CPU à 150 % n'est jamais franchissable et
/// produirait une règle silencieusement inerte. Mieux vaut refuser la règle à sa création.
/// </summary>
public sealed record Threshold
{
    public Threshold(ComparisonOperator comparison, double value, MonitoredMetric metric)
    {
        if (metric.IsStateBased())
        {
            throw new DomainValidationException(
                nameof(metric),
                $"la métrique {metric} est un état binaire et n'admet pas de seuil numérique");
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new DomainValidationException(nameof(value), "le seuil n'est pas un nombre fini");
        }

        if (metric.IsPercentage())
        {
            Guard.InRange(value, 0d, 100d, nameof(value));
        }
        else
        {
            Guard.NotNegative(value, nameof(value));
        }

        Comparison = comparison;
        Value = value;
        Metric = metric;
    }

    public ComparisonOperator Comparison { get; }

    public double Value { get; }

    public MonitoredMetric Metric { get; }

    /// <summary>
    /// Évalue une valeur observée contre ce seuil.
    /// </summary>
    public bool IsBreachedBy(double observed) => Comparison switch
    {
        ComparisonOperator.GreaterThan => observed > Value,
        ComparisonOperator.GreaterThanOrEqual => observed >= Value,
        ComparisonOperator.LessThan => observed < Value,
        ComparisonOperator.LessThanOrEqual => observed <= Value,
        _ => false,
    };

    public override string ToString()
    {
        var symbol = Comparison switch
        {
            ComparisonOperator.GreaterThan => ">",
            ComparisonOperator.GreaterThanOrEqual => "≥",
            ComparisonOperator.LessThan => "<",
            ComparisonOperator.LessThanOrEqual => "≤",
            _ => "?",
        };

        return string.Create(CultureInfo.InvariantCulture, $"{Metric} {symbol} {Value:0.##}");
    }
}
