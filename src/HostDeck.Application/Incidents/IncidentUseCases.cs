using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Errors;
using HostDeck.Application.Logging;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Ports;
using HostDeck.Domain.Incidents;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Incidents;

/// <summary>Liste les incidents correspondant à un filtre.</summary>
public sealed class GetIncidentsUseCase
{
    private readonly IIncidentRepository _incidents;

    public GetIncidentsUseCase(IIncidentRepository incidents)
    {
        _incidents = incidents;
    }

    public Task<IReadOnlyList<IncidentDto>> ExecuteAsync(
        IncidentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return _incidents.QueryAsync(filter, cancellationToken);
    }
}

/// <summary>Renvoie le détail d'un incident, chronologie comprise.</summary>
public sealed class GetIncidentDetailsUseCase
{
    private readonly IIncidentRepository _incidents;
    private readonly IAlertRuleRepository _rules;
    private readonly IServerRepository _servers;
    private readonly IClock _clock;

    public GetIncidentDetailsUseCase(
        IIncidentRepository incidents,
        IAlertRuleRepository rules,
        IServerRepository servers,
        IClock clock)
    {
        _incidents = incidents;
        _rules = rules;
        _servers = servers;
        _clock = clock;
    }

    public async Task<IncidentDetailsDto> ExecuteAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var id = new IncidentId(incidentId);

        var incident = await _incidents.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Incident), incidentId.ToString());

        var rule = await _rules.FindAsync(incident.RuleId, cancellationToken).ConfigureAwait(false);
        var server = await _servers.FindAsync(incident.ServerId, cancellationToken).ConfigureAwait(false);
        var timeline = await _incidents.GetTimelineAsync(id, cancellationToken).ConfigureAwait(false);

        return new IncidentDetailsDto
        {
            Incident = IncidentMapper.ToDto(incident, server?.Name.Value, rule, _clock.UtcNow),
            RuleId = incident.RuleId.Value,

            // La règle peut avoir été supprimée après l'ouverture de l'incident. L'incident
            // reste consultable : son historique ne doit pas disparaître avec sa règle.
            RuleName = rule?.Name ?? "Règle supprimée",
            RuleDescription = rule?.Description,
            Timeline = timeline,
            RecoveredAt = incident.RecoveredAt,
            ResolvedAt = incident.ResolvedAt,
        };
    }
}

/// <summary>Acquitte un incident au nom d'un opérateur.</summary>
public sealed class AcknowledgeIncidentUseCase
{
    private readonly IIncidentRepository _incidents;
    private readonly IMonitoringEventBus _events;
    private readonly IClock _clock;
    private readonly ILogger<AcknowledgeIncidentUseCase> _logger;

    public AcknowledgeIncidentUseCase(
        IIncidentRepository incidents,
        IMonitoringEventBus events,
        IClock clock,
        ILogger<AcknowledgeIncidentUseCase> logger)
    {
        _incidents = incidents;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        AcknowledgeIncidentDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = new IncidentId(request.IncidentId);
        var incident = await _incidents.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Incident), request.IncidentId.ToString());

        var now = _clock.UtcNow;

        // La transition est refusée par le domaine si l'incident est déjà résolu ou déjà
        // acquitté : c'est lui qui garantit la cohérence, pas ce use case.
        incident.Acknowledge(request.AcknowledgedBy, now);

        await _incidents.UpdateAsync(incident, cancellationToken).ConfigureAwait(false);
        await _incidents.AppendEventAsync(
            id,
            new IncidentEventDto
            {
                OccurredAt = now,
                Kind = IncidentEventKind.Acknowledged,
                Actor = request.AcknowledgedBy,
                Detail = request.Note,
            },
            cancellationToken).ConfigureAwait(false);

        _events.Publish(new IncidentChangedEvent(
            id,
            incident.ServerId,
            incident.Status,
            incident.Severity,
            IncidentChangeKind.Acknowledged,
            now));

        ApplicationLog.IncidentAcknowledged(_logger, request.IncidentId, request.AcknowledgedBy);
    }
}

/// <summary>Clôt un incident.</summary>
public sealed class ResolveIncidentUseCase
{
    private readonly IIncidentRepository _incidents;
    private readonly IMonitoringEventBus _events;
    private readonly IClock _clock;
    private readonly ILogger<ResolveIncidentUseCase> _logger;

    public ResolveIncidentUseCase(
        IIncidentRepository incidents,
        IMonitoringEventBus events,
        IClock clock,
        ILogger<ResolveIncidentUseCase> logger)
    {
        _incidents = incidents;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var id = new IncidentId(incidentId);
        var incident = await _incidents.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Incident), incidentId.ToString());

        var now = _clock.UtcNow;
        incident.Resolve(now);

        await _incidents.UpdateAsync(incident, cancellationToken).ConfigureAwait(false);
        await _incidents.AppendEventAsync(
            id,
            new IncidentEventDto { OccurredAt = now, Kind = IncidentEventKind.Resolved },
            cancellationToken).ConfigureAwait(false);

        _events.Publish(new IncidentChangedEvent(
            id,
            incident.ServerId,
            incident.Status,
            incident.Severity,
            IncidentChangeKind.Resolved,
            now));

        ApplicationLog.IncidentResolved(_logger, incidentId);
    }
}
