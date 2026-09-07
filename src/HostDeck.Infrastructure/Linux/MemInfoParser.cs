using System;
using System.Collections.Generic;
using System.Globalization;
using HostDeck.Application.Errors;
using HostDeck.Domain.Monitoring;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/proc/meminfo</c>.
///
/// <para>
/// Les valeurs y sont exprimées en kibioctets. La mémoire « utilisée » exclut le cache et
/// les tampons, qui sont récupérables : c'est la décomposition qu'affichent les maquettes
/// (utilisée / cache / tampon / libre) et celle qui évite de faire paraître saturée une
/// machine saine (§15).
/// </para>
///
/// <para>
/// Seules <c>MemTotal</c> et <c>MemFree</c> sont indispensables. Un hôte sans swap, ou un
/// fichier ne rapportant pas une ligne annexe, reste un cas normal et non une erreur.
/// </para>
/// </summary>
internal static class MemInfoParser
{
    private const string SourceName = "/proc/meminfo";

    /// <summary>Multiplicateur : /proc/meminfo rapporte des kibioctets.</summary>
    private const long BytesPerKilobyte = 1024;

    public static MemInfoSnapshot Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var values = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var valueText = line[(separator + 1)..].Trim();

            if (TryParseKibibytes(valueText, out var kibibytes))
            {
                values[key] = kibibytes;
            }
        }

        if (!values.TryGetValue("MemTotal", out var totalKib))
        {
            throw new MetricParseException(SourceName, "ligne « MemTotal » absente");
        }

        var freeKib = Get(values, "MemFree");
        var cachedKib = Get(values, "Cached");
        var buffersKib = Get(values, "Buffers");

        var usedKib = totalKib - freeKib - cachedKib - buffersKib;
        usedKib = Math.Max(0, Math.Min(totalKib, usedKib));

        var memory = new MemoryUsage(
            ByteSize.FromKibibytes(totalKib),
            ByteSize.FromKibibytes(usedKib),
            ByteSize.FromKibibytes(cachedKib),
            ByteSize.FromKibibytes(buffersKib));

        var swapTotalKib = Get(values, "SwapTotal");
        if (swapTotalKib <= 0)
        {
            return new MemInfoSnapshot { Memory = memory, Swap = SwapUsage.None };
        }

        var swapFreeKib = Get(values, "SwapFree");
        var swapUsedKib = Math.Clamp(swapTotalKib - swapFreeKib, 0, swapTotalKib);

        return new MemInfoSnapshot
        {
            Memory = memory,
            Swap = new SwapUsage(
                ByteSize.FromKibibytes(swapTotalKib),
                ByteSize.FromKibibytes(swapUsedKib)),
        };
    }

    private static bool TryParseKibibytes(string text, out long value)
    {
        // La ligne est « 1024000 kB ». On ignore l'unité, quelle qu'elle soit.
        value = 0;
        var fields = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 0
            && long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    private static long Get(Dictionary<string, long> values, string key) =>
        values.TryGetValue(key, out var value) ? value : 0;
}
