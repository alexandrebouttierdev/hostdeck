using System;
using System.Collections.Generic;

namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Parse la sortie de <c>/etc/os-release</c>.
///
/// <para>
/// Les lignes suivent le format <c>KEY=VALUE</c> de systemd, valeurs éventuellement entre
/// guillemets. Les lignes inconnues sont ignorées, ce qui rend le parse tolérant aux
/// distributions qui ajoutent leurs propres champs.
/// </para>
/// </summary>
internal static class OsReleaseParser
{
    public static OsReleaseInfo Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            values[key] = Unquote(line[(separator + 1)..].Trim());
        }

        return new OsReleaseInfo(
            Get(values, "ID") ?? "linux",
            Get(values, "NAME") ?? "Linux",
            Get(values, "VERSION") ?? Get(values, "VERSION_ID"),
            Get(values, "PRETTY_NAME"));
    }

    private static string? Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
