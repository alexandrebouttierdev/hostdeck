using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

/// <summary>
/// Vérifie que la migration initiale crée bien le schéma du §18 : les tables métier et les
/// index attendus, sur une vraie base SQLite neuve.
/// </summary>
public sealed class MigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitializeAsync_CreatesExpectedTables()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();

        await using var context = await database.Factory.CreateDbContextAsync(Ct);
        var connection = (SqliteConnection)context.Database.GetDbConnection();

        var names = await ReadStringsAsync(
            connection,
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name != '__EFMigrationsHistory'
              AND name != '__EFMigrationsLock'
            ORDER BY name
            """);

        Assert.Equal(
            [
                "alert_rules",
                "disk_metric_samples",
                "docker_containers",
                "docker_hosts",
                "docker_metric_samples",
                "incident_events",
                "incidents",
                "metric_samples",
                "network_metric_samples",
                "server_tags",
                "servers",
                "settings",
                "ssh_host_keys",
            ],
            names);
    }

    [Fact]
    public async Task InitializeAsync_CreatesExpectedIndexes()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();

        await using var context = await database.Factory.CreateDbContextAsync(Ct);
        var connection = (SqliteConnection)context.Database.GetDbConnection();

        var names = await ReadStringsAsync(
            connection,
            """
            SELECT name FROM sqlite_master
            WHERE type = 'index' AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """);

        Assert.Contains("ix_servers_endpoint", names);
        Assert.Contains("ix_metric_samples_server_time", names);
        Assert.Contains("ix_metric_samples_time", names);
        Assert.Contains("ix_incidents_server_rule_status", names);
        Assert.Contains("ix_disk_metric_samples_server_time", names);
        Assert.Contains("ix_network_metric_samples_server_time", names);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();
        await database.InitializeAsync();

        await using var context = await database.Factory.CreateDbContextAsync(Ct);
        Assert.True(await context.Database.CanConnectAsync(Ct));
    }

    private static async Task<List<string>> ReadStringsAsync(
        SqliteConnection connection,
        string sql)
    {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        // CA2100 : SQL entièrement constant, défini à chaque appel d'un littéral du test ;
        // aucune valeur d'entrée n'y est jamais composée.
#pragma warning disable CA2100
        command.CommandText = sql;
#pragma warning restore CA2100

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
