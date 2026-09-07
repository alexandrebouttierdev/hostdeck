using System;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Ports;
using HostDeck.Infrastructure.Credentials;
using HostDeck.Infrastructure.Monitoring;
using HostDeck.Infrastructure.Persistence;
using HostDeck.Infrastructure.Persistence.Repositories;
using HostDeck.Infrastructure.Persistence.TimeSeries;
using HostDeck.Infrastructure.Ssh;
using Latchkey;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostDeck.Infrastructure;

/// <summary>
/// Enregistre les adaptateurs d'infrastructure.
///
/// La couche décrit elle-même ce qu'elle fournit ; le composition root l'appelle sans avoir à
/// connaître EF Core, SQLite ni aucun type concret (§6).
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddHostDeckInfrastructure(
        this IServiceCollection services,
        Action<DatabaseOptions>? configureDatabase = null,
        Action<SshConnectionOptions>? configureSsh = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var databaseOptions = new DatabaseOptions();
        configureDatabase?.Invoke(databaseOptions);
        services.AddSingleton(databaseOptions);

        services.AddOptions<SshConnectionOptions>()
            .Configure(options => configureSsh?.Invoke(options));

        var connectionString = BuildConnectionString(databaseOptions);

        // Une fabrique plutôt qu'un contexte partagé : chaque opération obtient un contexte
        // neuf, dont le suivi de changements meurt avec elle. Un contexte de longue durée
        // finirait par retenir tout l'inventaire et pourrait écrire une mutation partielle
        // laissée par une opération en échec (§37, §50).
        services.AddDbContextFactory<HostDeckDbContext>(options =>
            options
                .UseSqlite(connectionString, sqlite =>
                    sqlite.CommandTimeout((int)databaseOptions.BusyTimeout.TotalSeconds))
                // Les PRAGMA sont propres à la connexion : l'intercepteur les repose sur
                // chacune de celles que le pool ouvre, pas seulement sur la première.
                .AddInterceptors(new SqlitePragmaInterceptor(databaseOptions.BusyTimeout)));

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IServerRepository, ServerRepository>();
        services.AddSingleton<IAlertRuleRepository, AlertRuleRepository>();
        services.AddSingleton<IIncidentRepository, IncidentRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IMetricsRepository, MetricsRepository>();
        services.AddSingleton<IHostKeyStore, HostKeyStore>();

        services.AddSingleton<ILatchkey>(_ => LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = OsCredentialStore.ServiceName,
            DisplayName = "HostDeck",
        }));
        services.AddSingleton<ICredentialStore, OsCredentialStore>();

        services.AddSingleton<SshCredentialLoader>();
        services.AddSingleton<ISshConnectionFactory, SshConnectionFactory>();

        services.AddSingleton<LinuxMetricCollector>();
        services.AddSingleton<IncidentEvaluationEngine>();
        services.AddHostedService<FleetMonitoringCoordinator>();

        return services;
    }

    /// <summary>
    /// Applique les migrations et prépare la base locale. À appeler au démarrage du composition root.
    /// </summary>
    public static Task InitializeHostDeckDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var initializer = services.GetRequiredService<DatabaseInitializer>();
        return initializer.InitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Construit la chaîne de connexion SQLite.
    ///
    /// <c>Cache=Shared</c> n'est volontairement pas activé : en mode WAL, chaque connexion
    /// dispose de son propre instantané de lecture, et un cache partagé introduirait des
    /// verrous au niveau des tables qui sérialiseraient les lectures avec les écritures.
    /// </summary>
    internal static string BuildConnectionString(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new SqliteConnectionStringBuilder
        {
            DataSource = options.ResolveDatabasePath(),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
        }.ToString();
    }
}
