using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Infrastructure.Persistence.Mapping;
using Microsoft.EntityFrameworkCore;

namespace HostDeck.Infrastructure.Persistence.Repositories;

/// <summary>Dépôt des règles d'alerte.</summary>
internal sealed class AlertRuleRepository : IAlertRuleRepository
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly IClock _clock;

    public AlertRuleRepository(IDbContextFactory<HostDeckDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.AlertRules
            .AsNoTracking()
            .OrderBy(record => record.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(AlertRuleRecordMapper.ToDomain)];
    }

    public async Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.AlertRules
            .AsNoTracking()
            .Where(record => record.IsEnabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(AlertRuleRecordMapper.ToDomain)];
    }

    public async Task<AlertRule?> FindAsync(AlertRuleId id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.AlertRules
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : AlertRuleRecordMapper.ToDomain(record);
    }

    public async Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.AlertRules.Add(AlertRuleRecordMapper.ToRecord(rule, _clock.UtcNow));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.AlertRules
            .FirstOrDefaultAsync(entity => entity.Id == rule.Id.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AlertRule), rule.Id.ToString());

        AlertRuleRecordMapper.ApplyTo(record, rule, _clock.UtcNow);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Supprime la règle. Les incidents qu'elle a ouverts sont conservés : effacer
    /// l'historique parce que la règle disparaît ferait perdre la trace de pannes réelles.
    /// </summary>
    public async Task DeleteAsync(AlertRuleId id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var deleted = await context.AlertRules
            .Where(record => record.Id == id.Value)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted == 0)
        {
            throw new EntityNotFoundException(nameof(AlertRule), id.ToString());
        }
    }
}

/// <summary>Dépôt des paramètres globaux.</summary>
internal sealed class SettingsRepository : ISettingsRepository
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;

    public SettingsRepository(IDbContextFactory<HostDeckDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Lit les paramètres, ou renvoie les valeurs par défaut tant qu'aucun enregistrement
    /// n'existe. Une installation neuve démarre ainsi sans écriture préalable.
    /// </summary>
    public async Task<SettingsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? SettingsSnapshot.Default : SettingsRecordMapper.ToSnapshot(record);
    }

    public async Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Settings
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new Records.SettingsRecord();
            context.Settings.Add(record);
        }

        SettingsRecordMapper.ApplyTo(record, settings);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
