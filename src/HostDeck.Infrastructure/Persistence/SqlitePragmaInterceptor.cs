using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HostDeck.Infrastructure.Persistence;

/// <summary>
/// Pose les PRAGMA SQLite sur chaque connexion ouverte par EF Core.
///
/// <para>
/// Un intercepteur est nécessaire parce que les PRAGMA sont propres à la connexion et non à
/// la base. Le pool en ouvre de nouvelles tout au long de la vie du processus : les appliquer
/// une seule fois au démarrage laisserait toutes les connexions suivantes en réglages par
/// défaut, donc sans WAL ni délai d'attente sur verrou.
/// </para>
///
/// <para>
/// <c>foreign_keys</c> est déjà activé par la chaîne de connexion, mais reste posé ici pour
/// que la garantie ne dépende pas d'une option de chaîne qu'un appelant pourrait omettre.
/// </para>
/// </summary>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private readonly TimeSpan _busyTimeout;

    public SqlitePragmaInterceptor(TimeSpan busyTimeout)
    {
        _busyTimeout = busyTimeout;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Apply(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Apply(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void Apply(DbConnection connection)
    {
        if (connection is SqliteConnection sqlite)
        {
            SqliteConnectionConfiguration.Apply(sqlite, _busyTimeout);
        }
    }
}
