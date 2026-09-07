using System;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Logging;

/// <summary>
/// Messages de journalisation de la couche Application, générés à la compilation par
/// <c>[LoggerMessage]</c>.
///
/// Ce n'est pas une coquetterie : HostDeck journalise dans la boucle de collecte, exécutée
/// pour chaque hôte à chaque cycle. Les surcharges à paramètres variables de
/// <c>ILogger</c> encadrent chaque appel, allouent un tableau et évaluent leurs arguments
/// même lorsque le niveau est désactivé. Les délégués générés ne font ni l'un ni l'autre
/// (§37, CPU faible au repos).
///
/// Aucun de ces messages ne prend de secret en paramètre : seuls des identifiants, des
/// adresses et des durées y figurent (§51, T2).
/// </summary>
internal static partial class ApplicationLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Server {ServerId} added ({Address}:{Port}, mode {ConnectionMode})")]
    public static partial void ServerAdded(
        ILogger logger,
        Guid serverId,
        string address,
        int port,
        Domain.Servers.ConnectionMode connectionMode);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Server {ServerId} updated")]
    public static partial void ServerUpdated(ILogger logger, Guid serverId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Server {ServerId} deleted")]
    public static partial void ServerDeleted(ILogger logger, Guid serverId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Failed to remove orphan credential {CredentialKey} after a failed insert")]
    public static partial void OrphanCredentialNotRemoved(
        ILogger logger,
        Exception exception,
        string credentialKey);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Server {ServerId} was deleted but its credential {CredentialKey} remains in the keychain")]
    public static partial void CredentialLeftBehind(
        ILogger logger,
        Exception exception,
        Guid serverId,
        string credentialKey);

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Incident {IncidentId} acknowledged by {AcknowledgedBy}")]
    public static partial void IncidentAcknowledged(
        ILogger logger,
        Guid incidentId,
        string acknowledgedBy);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Incident {IncidentId} resolved")]
    public static partial void IncidentResolved(ILogger logger, Guid incidentId);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Incident {IncidentId} opened on server {ServerId} by rule {RuleId} (severity {Severity})")]
    public static partial void IncidentOpened(
        ILogger logger,
        Guid incidentId,
        Guid serverId,
        Guid ruleId,
        Domain.Incidents.Severity severity);

    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Alert rule {RuleId} created ({Metric}, severity {Severity})")]
    public static partial void AlertRuleCreated(
        ILogger logger,
        Guid ruleId,
        Domain.Alerts.MonitoredMetric metric,
        Domain.Incidents.Severity severity);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Alert rule {RuleId} updated")]
    public static partial void AlertRuleUpdated(ILogger logger, Guid ruleId);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Information,
        Message = "Alert rule {RuleId} deleted")]
    public static partial void AlertRuleDeleted(ILogger logger, Guid ruleId);

    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Debug,
        Message = "Connection test for {Address}:{Port} finished with {Outcome} at stage {Stage} in {ElapsedMs} ms")]
    public static partial void ConnectionTested(
        ILogger logger,
        string address,
        int port,
        Dtos.Servers.TestConnectionOutcome outcome,
        Dtos.Servers.TestConnectionStage stage,
        long elapsedMs);

    [LoggerMessage(
        EventId = 1400,
        Level = LogLevel.Information,
        Message = "Settings updated")]
    public static partial void SettingsUpdated(ILogger logger);

    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Information,
        Message = "Host key approved for {Host}:{Port}")]
    public static partial void HostKeyApproved(ILogger logger, string host, int port);

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Warning,
        Message = "Host key change detected for server {ServerId} ({Host})")]
    public static partial void HostKeyChanged(ILogger logger, Guid serverId, string host);
}
