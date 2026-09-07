using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Ports;
using HostDeck.Infrastructure;
using HostDeck.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HostDeck.Infrastructure.Persistence.Repositories;

/// <summary>
/// Empreintes de clés d'hôte SSH approuvées.
///
/// Ce dépôt porte une décision de sécurité, pas une simple donnée de configuration : une
/// écriture ici signifie qu'un opérateur a explicitement approuvé une empreinte. Aucun chemin
/// de code n'approuve automatiquement (§51, T3).
/// </summary>
internal sealed class HostKeyStore : IHostKeyStore
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly IClock _clock;
    private readonly ILogger<HostKeyStore> _logger;

    public HostKeyStore(
        IDbContextFactory<HostDeckDbContext> contextFactory,
        IClock clock,
        ILogger<HostKeyStore> logger)
    {
        _contextFactory = contextFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task<string?> FindApprovedFingerprintAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.SshHostKeys
            .AsNoTracking()
            .Where(record => record.Host == host && record.Port == port)
            .Select(record => record.Fingerprint)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ApproveAsync(
        string host,
        int port,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.SshHostKeys
            .FirstOrDefaultAsync(entity => entity.Host == host && entity.Port == port, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            context.SshHostKeys.Add(new SshHostKeyRecord
            {
                Host = host,
                Port = port,
                Fingerprint = fingerprint,
                ApprovedAt = _clock.UtcNow,
            });
        }
        else
        {
            // Remplacer une empreinte déjà approuvée est journalisé en avertissement : c'est
            // la trace qu'une clé d'hôte a changé et que quelqu'un l'a acceptée.
            if (!string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                InfrastructureLog.HostKeyReplaced(_logger, host, port);
            }

            record.Fingerprint = fingerprint;
            record.ApprovedAt = _clock.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.SshHostKeys
            .Where(record => record.Host == host && record.Port == port)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
