using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Errors;
using HostDeck.Application.Incidents;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Persistence.Mapping;
using HostDeck.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace HostDeck.Infrastructure.Persistence.Repositories;

/// <summary>Dépôt des incidents et de leur chronologie.</summary>
internal sealed class IncidentRepository : IIncidentRepository
{
    private readonly IDbContextFactory<HostDeckDbContext> _contextFactory;
    private readonly IClock _clock;

    public IncidentRepository(IDbContextFactory<HostDeckDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Incident>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.Incidents
            .AsNoTracking()
            .Where(record =>
                record.Status == (int)IncidentStatus.Open
                || record.Status == (int)IncidentStatus.Acknowledged)
            .OrderByDescending(record => record.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(IncidentRecordMapper.ToDomain)];
    }

    /// <summary>
    /// Projette directement en DTO, avec le nom d'hôte et le libellé du problème.
    ///
    /// La jointure est faite ici plutôt qu'en mémoire : la table des incidents actifs se
    /// rafraîchit en continu, et charger séparément chaque serveur produirait une requête
    /// par ligne à chaque cycle (§37).
    /// </summary>
    public async Task<IReadOnlyList<IncidentDto>> QueryAsync(
        IncidentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = from incident in context.Incidents.AsNoTracking()
                    join server in context.Servers.AsNoTracking()
                        on incident.ServerId equals server.Id into servers
                    from server in servers.DefaultIfEmpty()
                    join rule in context.AlertRules.AsNoTracking()
                        on incident.RuleId equals rule.Id into rules
                    from rule in rules.DefaultIfEmpty()
                    select new { incident, server, rule };

        if (filter.Severities.Count > 0)
        {
            var severities = filter.Severities.Select(severity => (int)severity).ToList();
            query = query.Where(row => severities.Contains(row.incident.Severity));
        }

        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.Select(status => (int)status).ToList();
            query = query.Where(row => statuses.Contains(row.incident.Status));
        }

        if (filter.ServerId is { } serverId)
        {
            query = query.Where(row => row.incident.ServerId == serverId);
        }

        if (filter.From is { } from)
        {
            query = query.Where(row => row.incident.StartedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(row => row.incident.StartedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            query = query.Where(row =>
                (row.server != null && EF.Functions.Like(row.server.Name, $"%{term}%"))
                || (row.rule != null && EF.Functions.Like(row.rule.Name, $"%{term}%")));
        }

        var rows = await query
            .OrderByDescending(row => row.incident.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        return
        [
            .. rows.Select(row => IncidentMapper.ToDto(
                IncidentRecordMapper.ToDomain(row.incident),
                row.server?.Name,
                row.rule is null ? null : AlertRuleRecordMapper.ToDomain(row.rule),
                now))
        ];
    }

    public async Task<Incident?> FindAsync(IncidentId id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : IncidentRecordMapper.ToDomain(record);
    }

    public async Task<Incident?> FindActiveAsync(
        ServerId serverId,
        AlertRuleId ruleId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Incidents
            .AsNoTracking()
            .Where(entity =>
                entity.ServerId == serverId.Value
                && entity.RuleId == ruleId.Value
                && (entity.Status == (int)IncidentStatus.Open
                    || entity.Status == (int)IncidentStatus.Acknowledged))
            .OrderByDescending(entity => entity.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : IncidentRecordMapper.ToDomain(record);
    }

    public async Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.Incidents.Add(IncidentRecordMapper.ToRecord(incident));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incident);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var record = await context.Incidents
            .FirstOrDefaultAsync(entity => entity.Id == incident.Id.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Incident), incident.Id.ToString());

        IncidentRecordMapper.ApplyTo(record, incident);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendEventAsync(
        IncidentId incidentId,
        IncidentEventDto incidentEvent,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.IncidentEvents.Add(IncidentRecordMapper.ToRecord(incidentId, incidentEvent));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IncidentEventDto>> GetTimelineAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var records = await context.IncidentEvents
            .AsNoTracking()
            .Where(record => record.IncidentId == incidentId.Value)
            .OrderByDescending(record => record.OccurredAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(IncidentRecordMapper.ToDto)];
    }

    /// <summary>
    /// Purge les incidents clos antérieurs à la coupure.
    ///
    /// Ne touche jamais aux incidents actifs, quelle que soit leur ancienneté : un incident
    /// ouvert depuis plus longtemps que la rétention reste un incident ouvert.
    /// </summary>
    public async Task<int> PruneResolvedOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Incidents
            .Where(record =>
                record.Status == (int)IncidentStatus.Resolved
                && record.ResolvedAt != null
                && record.ResolvedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
