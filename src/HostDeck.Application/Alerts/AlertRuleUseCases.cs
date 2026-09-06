using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Alerts;
using HostDeck.Application.Errors;
using HostDeck.Application.Logging;
using HostDeck.Application.Ports;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Alerts;

/// <summary>Liste les règles d'alerte, avec leur nombre d'incidents actifs.</summary>
public sealed class GetAlertRulesUseCase
{
    private readonly IAlertRuleRepository _rules;
    private readonly IIncidentRepository _incidents;

    public GetAlertRulesUseCase(IAlertRuleRepository rules, IIncidentRepository incidents)
    {
        _rules = rules;
        _incidents = incidents;
    }

    public async Task<IReadOnlyList<AlertRuleDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var rules = await _rules.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var active = await _incidents.GetActiveAsync(cancellationToken).ConfigureAwait(false);

        var activeByRule = active
            .GroupBy(incident => incident.RuleId)
            .ToDictionary(group => group.Key, group => group.Count());

        return
        [
            .. rules.Select(rule => AlertRuleMapper.ToDto(
                rule,
                activeByRule.GetValueOrDefault(rule.Id)))
        ];
    }
}

/// <summary>Crée une règle d'alerte.</summary>
public sealed class CreateAlertRuleUseCase
{
    private readonly IAlertRuleRepository _rules;
    private readonly IValidator<CreateAlertRuleDto> _validator;
    private readonly ILogger<CreateAlertRuleUseCase> _logger;

    public CreateAlertRuleUseCase(
        IAlertRuleRepository rules,
        IValidator<CreateAlertRuleDto> validator,
        ILogger<CreateAlertRuleUseCase> logger)
    {
        _rules = rules;
        _validator = validator;
        _logger = logger;
    }

    public async Task<AlertRuleDto> ExecuteAsync(
        CreateAlertRuleDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var rule = new AlertRule(
            AlertRuleId.New(),
            request.Name,
            request.Metric,
            request.Severity,
            AlertRuleMapper.ToScope(request.ScopeKind, request.ScopeGroup, request.ScopeServerId),
            AlertRuleMapper.ToThreshold(request.Metric, request.Comparison, request.ThresholdValue),
            request.Duration,
            request.Cooldown,
            request.Description,
            request.IsEnabled);

        await _rules.AddAsync(rule, cancellationToken).ConfigureAwait(false);

        ApplicationLog.AlertRuleCreated(_logger, rule.Id.Value, rule.Metric, rule.Severity);

        return AlertRuleMapper.ToDto(rule, activeIncidents: 0);
    }
}

/// <summary>Modifie une règle d'alerte existante.</summary>
public sealed class UpdateAlertRuleUseCase
{
    private readonly IAlertRuleRepository _rules;
    private readonly IValidator<UpdateAlertRuleDto> _validator;
    private readonly ILogger<UpdateAlertRuleUseCase> _logger;

    public UpdateAlertRuleUseCase(
        IAlertRuleRepository rules,
        IValidator<UpdateAlertRuleDto> validator,
        ILogger<UpdateAlertRuleUseCase> logger)
    {
        _rules = rules;
        _validator = validator;
        _logger = logger;
    }

    public async Task<AlertRuleDto> ExecuteAsync(
        UpdateAlertRuleDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var ruleId = new AlertRuleId(request.RuleId);
        var rule = await _rules.FindAsync(ruleId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AlertRule), request.RuleId.ToString());

        rule.Rename(request.Name);
        rule.ChangeDescription(request.Description);
        rule.ChangeSeverity(request.Severity);
        rule.ChangeScope(AlertRuleMapper.ToScope(request.ScopeKind, request.ScopeGroup, request.ScopeServerId));

        // La métrique d'une règle n'est pas modifiable : changer la grandeur surveillée
        // rendrait incohérent l'historique des incidents déjà ouverts par cette règle.
        // Le seuil est validé contre la métrique d'origine.
        rule.ChangeThreshold(
            AlertRuleMapper.ToThreshold(rule.Metric, request.Comparison, request.ThresholdValue));
        rule.ChangeTiming(request.Duration, request.Cooldown);

        if (request.IsEnabled)
        {
            rule.Enable();
        }
        else
        {
            rule.Disable();
        }

        await _rules.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

        ApplicationLog.AlertRuleUpdated(_logger, request.RuleId);

        return AlertRuleMapper.ToDto(rule, activeIncidents: 0);
    }
}

/// <summary>Supprime une règle d'alerte.</summary>
public sealed class DeleteAlertRuleUseCase
{
    private readonly IAlertRuleRepository _rules;
    private readonly ILogger<DeleteAlertRuleUseCase> _logger;

    public DeleteAlertRuleUseCase(IAlertRuleRepository rules, ILogger<DeleteAlertRuleUseCase> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var id = new AlertRuleId(ruleId);

        _ = await _rules.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(AlertRule), ruleId.ToString());

        // Les incidents ouverts par cette règle sont conservés : effacer l'historique parce
        // que la règle disparaît ferait perdre la trace de pannes réelles.
        await _rules.DeleteAsync(id, cancellationToken).ConfigureAwait(false);

        ApplicationLog.AlertRuleDeleted(_logger, ruleId);
    }
}

/// <summary>Conversions entre <see cref="AlertRule"/> et ses DTO.</summary>
public static class AlertRuleMapper
{
    public static AlertRuleDto ToDto(AlertRule rule, int activeIncidents)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new AlertRuleDto
        {
            RuleId = rule.Id.Value,
            Name = rule.Name,
            Description = rule.Description,
            Metric = rule.Metric,
            Severity = rule.Severity,
            Comparison = rule.Threshold?.Comparison,
            ThresholdValue = rule.Threshold?.Value,
            Duration = rule.Duration,
            Cooldown = rule.Cooldown,
            ScopeKind = rule.Scope.Kind,
            ScopeGroup = rule.Scope.Group?.Name,
            ScopeServerId = rule.Scope.ServerId?.Value,
            IsEnabled = rule.IsEnabled,
            ActiveIncidents = activeIncidents,
        };
    }

    public static AlertScope ToScope(AlertScopeKind kind, string? group, Guid? serverId) => kind switch
    {
        AlertScopeKind.Group when !string.IsNullOrWhiteSpace(group) =>
            AlertScope.ForGroup(new ServerGroup(group)),
        AlertScopeKind.Server when serverId is { } id =>
            AlertScope.ForServer(new ServerId(id)),
        _ => AlertScope.Global,
    };

    /// <summary>
    /// Construit le seuil, ou <c>null</c> pour une métrique d'état binaire qui n'en admet pas.
    /// </summary>
    public static Threshold? ToThreshold(
        MonitoredMetric metric,
        ComparisonOperator? comparison,
        double? value)
    {
        if (metric.IsStateBased())
        {
            return null;
        }

        if (comparison is not { } op || value is not { } threshold)
        {
            return null;
        }

        return new Threshold(op, threshold, metric);
    }
}
