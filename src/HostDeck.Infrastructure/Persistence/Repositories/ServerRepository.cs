using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Persistence.Mapping;
using HostDeck.Infrastructure.Persistence.Records;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HostDeck.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dépôt des serveurs, adossé à EF Core.
///
/// Chaque opération ouvre son propre contexte via la fabrique plutôt que de partager un
/// contexte de longue durée : le suivi de changements d'un contexte partagé finirait par
/// retenir tout l'inventaire, et une mutation partielle laissée par un use case en échec
/// pourrait être écrite par un enregistrement ultérieur (§37, §50).
/// </summary>
internal sealed class ServerRepository : IServerRepository
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly IClock _clock;

    public ServerRepository(IDbContextFactory<HostDeckDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.Servers
            .AsNoTracking()
            .Include(record => record.Tags)
            .OrderBy(record => record.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(ServerRecordMapper.ToDomain)];
    }

    public async Task<Server?> FindAsync(ServerId id, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Servers
            .AsNoTracking()
            .Include(entity => entity.Tags)
            .FirstOrDefaultAsync(entity => entity.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ServerRecordMapper.ToDomain(record);
    }

    public async Task AddAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        context.Servers.Add(ServerRecordMapper.ToRecord(server, _clock.UtcNow));

        await SaveAsync(context, "add server", cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Server server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Servers
            .Include(entity => entity.Tags)
            .FirstOrDefaultAsync(entity => entity.Id == server.Id.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Server), server.Id.ToString());

        // Les étiquettes existantes sont retirées explicitement : le mapper remplace la
        // collection, et EF ne supprimerait pas les lignes orphelines sans cela.
        context.ServerTags.RemoveRange(record.Tags);
        ServerRecordMapper.ApplyTo(record, server, _clock.UtcNow);

        await SaveAsync(context, "update server", cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(ServerId id, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        // Suppression par requête : les métriques, incidents et conteneurs liés partent en
        // cascade côté SQLite, sans charger une seule ligne en mémoire.
        var deleted = await context.Servers
            .Where(record => record.Id == id.Value)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted == 0)
        {
            throw new EntityNotFoundException(nameof(Server), id.ToString());
        }
    }

    public async Task<bool> ExistsWithEndpointAsync(
        HostAddress address,
        Port port,
        ServerId? excluding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        await using var context = await CreateContextAsync(cancellationToken).ConfigureAwait(false);

        var excludedId = excluding?.Value;

        return await context.Servers
            .AsNoTracking()
            .AnyAsync(
                record => record.Address == address.Value
                    && record.Port == port.Value
                    && (excludedId == null || record.Id != excludedId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<HostDeckDbContext> CreateContextAsync(CancellationToken cancellationToken) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    /// <summary>
    /// Enregistre les changements en traduisant les erreurs SQLite vers l'erreur applicative
    /// correspondante, de sorte qu'aucun type du fournisseur ne franchisse l'Infrastructure (§11).
    /// </summary>
    private static async Task SaveAsync(
        HostDeckDbContext context,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException)
        {
            throw new RepositoryException(operation, exception);
        }
    }
}
