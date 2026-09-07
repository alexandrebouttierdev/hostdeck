using System;
using System.Collections.Generic;
using System.Globalization;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/proc/net/dev</c>.
///
/// <para>
/// Chaque ligne de données porte le nom d'une interface puis, après les deux points, les
/// compteurs cumulés de réception et d'émission. Seuls les octets reçus et émis sont
/// retenus : le débit sera dérivé de deux relevés successifs par
/// <see cref="NetworkUsage.RateSince"/>.
/// </para>
///
/// <para>
/// Les en-têtes et les lignes illisibles sont ignorées plutôt que de faire échouer toute la
/// collecte : une interface dont une valeur serait corrompue ne doit pas masquer les autres
/// (§16). Toutes les interfaces sont conservées, boucle <c>lo</c> comprise — le filtrage,
/// s'il en faut un, relève du choix d'affichage, pas du parse.
/// </para>
/// </summary>
internal static class NetworkDevParser
{
    public static IReadOnlyList<NetworkUsage> Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var interfaces = new List<NetworkUsage>();

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var interfaceName = line[..separator].Trim();
            var valuesText = line[(separator + 1)..].Trim();
            var values = valuesText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Au moins le compteur de réception et celui d'émission.
            if (values.Length < 9
                || !TryParseCounter(values[0], out var received)
                || !TryParseCounter(values[8], out var transmitted))
            {
                continue;
            }

            interfaces.Add(new NetworkUsage(
                interfaceName,
                new ByteSize(received),
                new ByteSize(transmitted)));
        }

        return interfaces;
    }

    private static bool TryParseCounter(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
}
