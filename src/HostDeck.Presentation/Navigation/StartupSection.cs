using System;

namespace HostDeck.Presentation.Navigation;

/// <summary>
/// Résout la section initiale depuis les arguments CLI ou l'environnement
/// (<c>--section=Infrastructure</c> / <c>HOSTDECK_SECTION</c>). Utile pour les captures
/// visuelles (<c>scripts/screenshot.sh … -- --section=…</c>).
/// </summary>
public static class StartupSection
{
    public const string EnvironmentVariableName = "HOSTDECK_SECTION";

    public static ShellSection? TryParse(string[]? args)
    {
        if (args is not null)
        {
            foreach (var arg in args)
            {
                if (TryParseToken(arg, out var fromArg))
                {
                    return fromArg;
                }
            }
        }

        var env = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return TryParseName(env, out var fromEnv) ? fromEnv : null;
    }

    private static bool TryParseToken(string arg, out ShellSection section)
    {
        section = default;
        const string prefix = "--section=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseName(arg[prefix.Length..], out section);
        }

        return false;
    }

    private static bool TryParseName(string? name, out ShellSection section)
    {
        section = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return Enum.TryParse(name.Trim(), ignoreCase: true, out section)
            && Enum.IsDefined(section);
    }
}
