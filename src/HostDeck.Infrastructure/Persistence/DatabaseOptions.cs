using System;
using System.IO;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Emplacement et réglages de la base locale.
/// </summary>
public sealed class DatabaseOptions
{
    public const string DefaultFileName = "hostdeck.db";

    /// <summary>
    /// Répertoire contenant la base. Vide signifie le répertoire de données de
    /// l'utilisateur, ce qui garde le fichier sous son profil et donc soumis aux permissions
    /// par défaut de son compte (§51).
    /// </summary>
    public string? Directory { get; set; }

    public string FileName { get; set; } = DefaultFileName;

    /// <summary>
    /// Délai pendant lequel SQLite réessaie quand la base est verrouillée par un autre
    /// écrivain, avant de renvoyer une erreur.
    ///
    /// SQLite n'admet qu'un écrivain à la fois. Sans ce délai, une purge concurrente d'un
    /// cycle de collecte échouerait immédiatement sur SQLITE_BUSY (§49).
    /// </summary>
    public TimeSpan BusyTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Résout le chemin complet du fichier de base et s'assure que son répertoire existe.
    /// </summary>
    public string ResolveDatabasePath()
    {
        var directory = string.IsNullOrWhiteSpace(Directory)
            ? DefaultDirectory()
            : System.IO.Path.GetFullPath(Directory);

        System.IO.Directory.CreateDirectory(directory);

        return System.IO.Path.Combine(directory, FileName);
    }

    /// <summary>
    /// Répertoire de données propre à l'utilisateur, selon les conventions du système.
    /// </summary>
    public static string DefaultDirectory() => System.IO.Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create),
        "HostDeck");
}
