namespace HostDeck.Infrastructure.Ssh;

/// <summary>
/// Commandes SSH fixes utilisées pour la collecte.
///
/// Aucune de ces chaînes n'est assemblée depuis une saisie utilisateur : c'est la mesure
/// qui exclut l'injection (§15, §51 T4). Les chemins <c>/proc</c> et <c>/etc</c> sont lus
/// par <c>cat</c> ; <c>df</c> est appelé avec des options POSIX explicites.
/// </summary>
internal static class SshCommands
{
    public const string ReadCpuStat = "cat /proc/stat";

    public const string ReadMemInfo = "cat /proc/meminfo";

    public const string ReadLoadAverage = "cat /proc/loadavg";

    public const string ReadNetworkDev = "cat /proc/net/dev";

    public const string ReadUptime = "cat /proc/uptime";

    public const string ReadOsRelease = "cat /etc/os-release";

    public const string ReadDiskUsage = "df -P -k";

    public const string ReadHostname = "uname -n";

    public const string ReadKernel = "uname -r";

    /// <summary>Sonde légère utilisée par le test de connexion.</summary>
    public const string ProbeIdentity = "uname -s";
}
