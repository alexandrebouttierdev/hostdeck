using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Ports;
using HostDeck.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

/// <summary>
/// Base SQLite réelle, isolée dans un répertoire temporaire dédié.
///
/// Les tests posent les migrations exactement comme l'application au démarrage, puis
/// instancient les dépôts internes par le même câblage que la production
/// (<c>BuildConnectionString</c> + intercepteur de PRAGMA). Aucune base de l'utilisateur
/// n'est touchée : le fichier vit sous <c>/tmp</c> et est supprimé à la fin du test.
/// </summary>
internal sealed class TestDatabase : IAsyncLifetime
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "hostdeck-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Horloge figée : les tests ne dépendent jamais de l'heure réelle.</summary>
    public DateTimeOffset Now { get; } = new(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);

    public IClock Clock { get; }

    private IDbContextFactory<HostDeckDbContext> _factory = null!;

    public IDbContextFactory<HostDeckDbContext> Factory => _factory;

    public TestDatabase()
    {
        Clock = new FixedClock(Now);
    }

    public async ValueTask InitializeAsync()
    {
        var options = new DatabaseOptions { Directory = _directory };
        var connectionString = InfrastructureServiceCollectionExtensions.BuildConnectionString(options);

        var contextOptions = new DbContextOptionsBuilder<HostDeckDbContext>()
            .UseSqlite(connectionString, sqlite =>
                sqlite.CommandTimeout((int)options.BusyTimeout.TotalSeconds))
            .AddInterceptors(new SqlitePragmaInterceptor(options.BusyTimeout))
            .Options;

        _factory = new ContextFactory(contextOptions);

        var initializer = new DatabaseInitializer(
            _factory,
            options,
            NullLogger<DatabaseInitializer>.Instance);

        await initializer.InitializeAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Reliquat éventuel d'un fichier verrouillé : sans conséquence pour le test.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Fabrique minimale alignée sur la sémantique de <c>IDbContextFactory</c> : chaque
    /// opération reçoit un contexte neuf, comme en production (§37).
    /// </summary>
    private sealed class ContextFactory : IDbContextFactory<HostDeckDbContext>
    {
        private readonly DbContextOptions<HostDeckDbContext> _options;

        public ContextFactory(DbContextOptions<HostDeckDbContext> options)
        {
            _options = options;
        }

        public HostDeckDbContext CreateDbContext() => new(_options);

        public Task<HostDeckDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

/// <summary>Horloge renvoyant toujours le même instant.</summary>
internal sealed class FixedClock : IClock
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now)
    {
        _now = now;
    }

    public DateTimeOffset UtcNow => _now;
}
