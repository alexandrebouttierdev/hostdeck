using System;
using System.Globalization;
using HostDeck.Application.Errors;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/proc/uptime</c>, dont le premier champ est la durée de
/// fonctionnement en secondes.
/// </summary>
internal static class UptimeParser
{
    private const string SourceName = "/proc/uptime";

    public static Uptime Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var fields = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0
            || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || !double.IsFinite(seconds)
            || seconds < 0)
        {
            throw new MetricParseException(
                SourceName,
                "le premier champ n'est pas une durée valide");
        }

        return Uptime.FromSeconds(seconds);
    }
}
