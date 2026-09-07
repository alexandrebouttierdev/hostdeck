using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Prépare la base au démarrage : applique les migrations en attente et pose les PRAGMA.
/// </summary>
internal sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    /// <summary>
    /// Chemin résolu une seule fois : <see cref="DatabaseOptions.ResolveDatabasePath"/> crée
    /// le répertoire au passage, ce qui n'a pas sa place dans un appel de journalisation.
    /// </summary>
    private readonly string _databasePath;

    public DatabaseInitializer(
        IDbContextFactory<HostDeckDbContext> contextFactory,
        DatabaseOptions options,
        ILogger<DatabaseInitializer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _contextFactory = contextFactory;
        _databasePath = options.ResolveDatabasePath();
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException("apply migrations", exception);
        }

        InfrastructureLog.DatabaseReady(_logger, _databasePath);
    }
}

/// <summary>
/// Applique les PRAGMA SQLite à chaque connexion ouverte.
///
/// <para>
/// Les PRAGMA sont propres à la connexion, pas à la base : les poser une seule fois au
/// démarrage ne suffirait pas, car le pool de connexions en ouvre de nouvelles.
/// <c>foreign_keys</c> en particulier revient à OFF sur toute connexion neuve, ce qui
/// désactiverait silencieusement les suppressions en cascade.
/// </para>
/// </summary>
internal static class SqliteConnectionConfiguration
{
    public static void Apply(SqliteConnection connection, TimeSpan busyTimeout)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();

        // CA2100 : la seule valeur interpolée est un entier issu d'un TimeSpan de
        // configuration. Les PRAGMA n'acceptent pas de paramètres liés, donc l'interpolation
        // est ici la seule forme possible ; elle ne peut pas transporter de texte.
#pragma warning disable CA2100

        // WAL : autorise des lectures concurrentes pendant qu'un cycle de collecte écrit,
        // ce qui évite que l'interface se fige à chaque insertion de lot (§18).
        // NORMAL plutôt que FULL : sur une base de supervision locale, perdre les dernières
        // millisecondes de métriques après une coupure brutale est sans conséquence, alors
        // qu'un fsync par transaction coûterait cher à chaque cycle.
        command.CommandText = $"""
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = {(int)busyTimeout.TotalMilliseconds};
            PRAGMA temp_store = MEMORY;
            """;

        command.ExecuteNonQuery();
#pragma warning restore CA2100
    }
}
