using System;
using System.Collections.Generic;
using System.Linq;
using HostDeck.Application.Dtos.Monitoring;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Servers;

/// <summary>
/// Conversions entre les entités du domaine et les DTO de serveur.
///
/// Explicites et testées (§8) : un mapping automatique par convention casserait
/// silencieusement dès qu'une propriété serait renommée, et surtout il pourrait exposer un
/// champ que ces DTO ne doivent pas porter.
/// </summary>
public static class ServerMapper
{
    public static ServerSummaryDto ToSummary(Server server, LatestMetricDto? latest = null)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new ServerSummaryDto
        {
            ServerId = server.Id.Value,
            Name = server.Name.Value,
            Address = server.Address.Value,
            Status = server.Status,
            ConnectionMode = server.ConnectionMode,
            Group = server.Group?.Name,
            Tags = [.. server.Tags.Select(tag => tag.Value)],
            DockerEnabled = server.DockerEnabled,
            Latest = latest,
            LastCollectedAt = server.LastCollectedAt,
        };
    }

    /// <summary>
    /// Projette la configuration éditable. Ne renvoie que la <em>référence</em> du credential :
    /// le secret n'est jamais relu vers un DTO (§30).
    /// </summary>
    public static ServerConfigurationDto ToConfiguration(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new ServerConfigurationDto
        {
            ServerId = server.Id.Value,
            Name = server.Name.Value,
            Address = server.Address.Value,
            Port = server.Port.Value,
            Username = server.Username.Value,
            CredentialKey = server.Credential.Key,
            CredentialKind = server.Credential.Kind,
            MonitoringIntervalSeconds = (int)server.MonitoringInterval.Value.TotalSeconds,
            DockerEnabled = server.DockerEnabled,
            Group = server.Group?.Name,
            Tags = [.. server.Tags.Select(tag => tag.Value)],
            JumpHost = ToJumpHostDto(server.JumpHost),
        };
    }

    public static JumpHostDto? ToJumpHostDto(JumpHost? jumpHost) => jumpHost is null
        ? null
        : new JumpHostDto
        {
            Address = jumpHost.Address.Value,
            Port = jumpHost.Port.Value,
            Username = jumpHost.Username.Value,
            CredentialKey = jumpHost.Credential.Key,
            CredentialKind = jumpHost.Credential.Kind,
        };

    public static JumpHost? ToDomain(JumpHostDto? dto) => dto is null
        ? null
        : new JumpHost(
            HostAddress.Parse(dto.Address),
            new Port(dto.Port),
            new SshUsername(dto.Username),
            new CredentialReference(dto.CredentialKey, dto.CredentialKind));

    public static IReadOnlyList<ServerTag> ToTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return [.. tags.Select(tag => new ServerTag(tag))];
    }

    public static ServerGroup? ToGroup(string? group) =>
        string.IsNullOrWhiteSpace(group) ? null : new ServerGroup(group);
}
