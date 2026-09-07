using System;
using System.Globalization;
using HostDeck.Application.Errors;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/proc/loadavg</c>, dont les trois premiers champs sont les charges
/// moyennes sur 1, 5 et 15 minutes.
/// </summary>
internal static class LoadAverageParser
{
    private const string SourceName = "/proc/loadavg";

    public static LoadAverage Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var fields = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 3)
        {
            throw new MetricParseException(
                SourceName,
                "moins de trois champs numériques");
        }

        if (!TryParseDouble(fields[0], out var one)
            || !TryParseDouble(fields[1], out var five)
            || !TryParseDouble(fields[2], out var fifteen)
            || !double.IsFinite(one) || one < 0
            || !double.IsFinite(five) || five < 0
            || !double.IsFinite(fifteen) || fifteen < 0)
        {
            throw new MetricParseException(
                SourceName,
                "les trois premières valeurs ne sont pas des charges valides");
        }

        return new LoadAverage(one, five, fifteen);
    }

    private static bool TryParseDouble(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
