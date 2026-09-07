using System;
using System.Globalization;
using HostDeck.Application.Errors;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/proc/stat</c> en un <see cref="CpuTimes"/>.
///
/// <para>
/// Parser pur, sans SSH ni Avalonia (§16) : il reçoit le texte brut et n'a aucun effet de
/// bord. Les lignes inconnues sont ignorées ; seule l'absence totale de ligne agrégée est
/// une erreur de format explicite.
/// </para>
/// </summary>
internal static class CpuStatParser
{
    private const string SourceName = "/proc/stat";

    public static CpuTimes Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        CpuTimes? aggregate = null;
        var coreCount = 0;

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0)
            {
                continue;
            }

            if (fields[0] == "cpu")
            {
                aggregate = ParseAggregate(fields);
            }
            else if (fields[0].StartsWith("cpu", StringComparison.Ordinal)
                     && fields[0].Length > 3
                     && IsDigits(fields[0].AsSpan(3)))
            {
                coreCount++;
            }
        }

        if (aggregate is not { } cpuTimes)
        {
            throw new MetricParseException(SourceName, "aucune ligne agrégée « cpu »");
        }

        return new CpuTimes(
            cpuTimes.User,
            cpuTimes.Nice,
            cpuTimes.System,
            cpuTimes.Idle,
            cpuTimes.IoWait,
            cpuTimes.Irq,
            cpuTimes.SoftIrq,
            cpuTimes.Steal,
            coreCount);
    }

    private static CpuTimes ParseAggregate(string[] fields)
    {
        if (fields.Length < 5)
        {
            throw new MetricParseException(
                SourceName,
                "la ligne « cpu » ne porte pas assez de compteurs");
        }

        long At(int index) =>
            index < fields.Length && TryParseCounter(fields[index], out var value)
                ? value
                : 0;

        // Colonnes de /proc/stat : user nice system idle iowait irq softirq steal guest guest_nice.
        return new CpuTimes(
            At(1),
            At(2),
            At(3),
            At(4),
            At(5),
            At(6),
            At(7),
            At(8),
            coreCount: 0);
    }

    private static bool TryParseCounter(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return text.Length > 0;
    }
}
