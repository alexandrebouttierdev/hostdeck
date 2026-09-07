using System;
using System.Threading.Tasks;
using HostDeck.Application.Ports;
using HostDeck.Infrastructure.Persistence.Repositories;
using HostDeck.Infrastructure.Persistence.TimeSeries;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

/// <summary>
/// Base des tests de dépôts : chaque test reçoit une base SQLite neuve, migrée, isolée
/// dans un répertoire temporaire, et des dépôts câblés comme en production.
/// </summary>
public abstract class RepositoryTestBase : IAsyncLifetime
{
    private TestDatabase? _database;

    /// <summary>
    /// Jeton d'annulation du test en cours. Le propager garde la suite réactive à une
    /// interruption et exerce au passage les chemins d'annulation des dépôts.
    /// </summary>
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal TestDatabase Database => _database
        ?? throw new InvalidOperationException("InitializeAsync n'a pas été exécuté.");

    public async ValueTask InitializeAsync()
    {
        _database = new TestDatabase();
        await _database.InitializeAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _database?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    protected IServerRepository Servers =>
        new ServerRepository(Database.Factory, Database.Clock);

    protected IAlertRuleRepository AlertRules =>
        new AlertRuleRepository(Database.Factory, Database.Clock);

    protected ISettingsRepository Settings =>
        new SettingsRepository(Database.Factory);

    protected IIncidentRepository Incidents =>
        new IncidentRepository(Database.Factory, Database.Clock);

    protected IHostKeyStore HostKeys =>
        new HostKeyStore(
            Database.Factory,
            Database.Clock,
            NullLogger<HostKeyStore>.Instance);

    protected IMetricsRepository Metrics =>
        new MetricsRepository(
            Database.Factory,
            NullLogger<MetricsRepository>.Instance);
}
