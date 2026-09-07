namespace HostDeck.Infrastructure.Linux;

/// <summary>
/// Informations d'identification du système lues dans <c>/etc/os-release</c>.
/// </summary>
internal sealed record OsReleaseInfo
{
    public OsReleaseInfo(
        string id,
        string name,
        string? version,
        string? prettyName)
    {
        Id = id;
        Name = name;
        Version = string.IsNullOrWhiteSpace(version) ? null : version;
        PrettyName = string.IsNullOrWhiteSpace(prettyName) ? null : prettyName;
    }

    /// <summary>Identifiant court, par exemple « ubuntu » ou « debian ».</summary>
    public string Id { get; }

    /// <summary>Nom de la distribution, par exemple « Ubuntu ».</summary>
    public string Name { get; }

    /// <summary>Version, par exemple « 22.04.4 LTS ».</summary>
    public string? Version { get; }

    /// <summary>Libellé complet tel que la distribution le fournit, quand il existe.</summary>
    public string? PrettyName { get; }

    /// <summary>
    /// Nom lisible pour l'interface : le libellé complet s'il existe, sinon une composition
    /// du nom et de la version.
    /// </summary>
    public string DisplayName =>
        PrettyName ?? (Version is null ? Name : $"{Name} {Version}");
}
