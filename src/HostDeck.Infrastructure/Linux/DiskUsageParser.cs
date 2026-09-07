using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>df -P -k</c>.
///
/// <para>
/// Le format POSIX garanti par <c>-P</c> tient chaque point de montage sur une seule ligne,
/// avec des blocs de 1024 octets. L'option <c>-k</c> rend l'unité explicite, donc le parse
/// ne dépend pas de la taille de bloc du système de fichiers.
/// </para>
///
/// <para>
/// L'en-tête et les lignes illisibles sont ignorés. Tous les points de montage sont
/// conservés : c'est la partition la plus remplie qui déclenche un incident, et filtrer ici
/// déciderait à la place de l'utilisateur ce qui compte pour lui (§16).
/// </para>
/// </summary>
internal static class DiskUsageParser
{
    private const long BytesPerKiBBlock = 1024;

    private static readonly Regex DataLine = new(
        @"^\s*(\S+)\s+(\d+)\s+(\d+)\s+(\d+)\s+\S+\s+(.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<DiskUsage> Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var disks = new List<DiskUsage>();

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Ligne d'en-tête de df -P.
            if (line.StartsWith("Filesystem", StringComparison.Ordinal))
            {
                continue;
            }

            var match = DataLine.Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (!long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var totalKiB)
                || !long.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var usedKiB)
                || totalKiB < 0
                || usedKiB < 0
                || usedKiB > totalKiB)
            {
                continue;
            }

            disks.Add(new DiskUsage(
                match.Groups[5].Value,
                match.Groups[1].Value,
                ByteSize.FromKibibytes(totalKiB),
                ByteSize.FromKibibytes(usedKiB)));
        }

        return disks;
    }
}
