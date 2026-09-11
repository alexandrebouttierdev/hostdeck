using System;
using System.Globalization;
using System.Text.RegularExpressions;
using HostDeck.Application.Errors;

namespace HostDeck.Infrastructure.Docker;

/// <summary>
/// Commandes Docker CLI fixes exécutées via SSH.
/// </summary>
/// <remarks>
/// Le CLI Docker est préféré à un forward de socket Unix via SSH.NET : le forward local
/// est fragile et peu portable, alors que <c>docker … --format '{{json .}}'</c> renvoie
/// du JSON parseable avec System.Text.Json sans dépendre du SDK Docker.DotNet.
/// </remarks>
internal static partial class DockerCliCommands
{
    /// <summary>Empreinte hexadécimale d'un identifiant conteneur (12 à 64 caractères).</summary>
    [GeneratedRegex("^[a-fA-F0-9]{12,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerIdPattern();

    public const string Info = "docker info --format '{{json .}}'";

    public const string ListContainers = "docker ps -a --format '{{json .}}'";

    public const string StatsAll = "docker stats --no-stream --format '{{json .}}'";

    public static string Inspect(string containerId) =>
        $"docker inspect {RequireValidContainerId(containerId)} --format '{{{{json .}}}}'";

    public static string Stats(string containerId) =>
        $"docker stats --no-stream --format '{{{{json .}}}}' {RequireValidContainerId(containerId)}";

    public static string Start(string containerId) =>
        $"docker start {RequireValidContainerId(containerId)}";

    public static string Stop(string containerId, int gracePeriodSeconds) =>
        $"docker stop -t {RequireGracePeriod(gracePeriodSeconds)} {RequireValidContainerId(containerId)}";

    public static string Restart(string containerId, int gracePeriodSeconds) =>
        $"docker restart -t {RequireGracePeriod(gracePeriodSeconds)} {RequireValidContainerId(containerId)}";

    public static string LogsTail(int tailLines, bool timestamps, string containerId)
    {
        var tail = RequireTailLines(tailLines);
        var prefix = timestamps ? "docker logs --timestamps --tail " : "docker logs --tail ";
        return $"{prefix}{tail} {RequireValidContainerId(containerId)}";
    }

    /// <summary>
    /// Valide un identifiant conteneur avant interpolation dans une commande.
    /// </summary>
    public static string RequireValidContainerId(string containerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerId);

        var trimmed = containerId.Trim();
        if (!ContainerIdPattern().IsMatch(trimmed))
        {
            throw new DockerException(
                "validate-container-id",
                "l'identifiant conteneur doit être une empreinte hexadécimale de 12 à 64 caractères");
        }

        return trimmed;
    }

    private static int RequireGracePeriod(int gracePeriodSeconds)
    {
        if (gracePeriodSeconds is < 0 or > 300)
        {
            throw new DockerException(
                "validate-grace-period",
                "le délai d'arrêt doit être compris entre 0 et 300 secondes");
        }

        return gracePeriodSeconds;
    }

    private static int RequireTailLines(int tailLines)
    {
        if (tailLines is < 1 or > 10_000)
        {
            throw new DockerException(
                "validate-tail-lines",
                "le nombre de lignes de journal doit être compris entre 1 et 10 000");
        }

        return tailLines;
    }
}
