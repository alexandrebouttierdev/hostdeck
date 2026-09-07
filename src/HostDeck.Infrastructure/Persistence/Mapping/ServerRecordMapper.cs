using System;
using System.Linq;
using HostDeck.Domain.Servers;
using HostDeck.Infrastructure.Persistence.Records;

namespace HostDeck.Infrastructure.Persistence.Mapping;

/// <summary>
/// Conversions entre <see cref="Server"/> et <see cref="ServerRecord"/>.
///
/// Explicites plutôt que par convention : un mapping automatique casserait silencieusement au
/// premier renommage, et surtout il pourrait faire transiter un champ que le record ne doit
/// pas porter. La conversion vers le domaine repasse par les constructeurs, donc une ligne
/// corrompue en base est rejetée au lieu de produire une entité invalide (§8, §18).
/// </summary>
internal static class ServerRecordMapper
{
    public static ServerRecord ToRecord(Server server, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(server);

        var record = new ServerRecord
        {
            Id = server.Id.Value,
            CreatedAt = now,
        };

        ApplyTo(record, server, now);
        return record;
    }

    /// <summary>
    /// Reporte l'état du domaine sur une ligne existante, sans toucher à
    /// <see cref="ServerRecord.CreatedAt"/>.
    /// </summary>
    public static void ApplyTo(ServerRecord record, Server server, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(server);

        record.Name = server.Name.Value;
        record.Address = server.Address.Value;
        record.Port = server.Port.Value;
        record.Username = server.Username.Value;
        record.CredentialKey = server.Credential.Key;
        record.CredentialKind = (int)server.Credential.Kind;
        record.MonitoringIntervalSeconds = (int)server.MonitoringInterval.Value.TotalSeconds;
        record.DockerEnabled = server.DockerEnabled;
        record.GroupName = server.Group?.Name;
        record.Status = (int)server.Status;
        record.LastCollectedAt = server.LastCollectedAt;
        record.UpdatedAt = now;

        record.JumpHostAddress = server.JumpHost?.Address.Value;
        record.JumpHostPort = server.JumpHost?.Port.Value;
        record.JumpHostUsername = server.JumpHost?.Username.Value;
        record.JumpHostCredentialKey = server.JumpHost?.Credential.Key;
        record.JumpHostCredentialKind = server.JumpHost is null
            ? null
            : (int)server.JumpHost.Credential.Kind;

        record.Tags.Clear();
        record.Tags.AddRange(server.Tags.Select(tag => new ServerTagRecord
        {
            ServerId = server.Id.Value,
            Tag = tag.Value,
        }));
    }

    public static Server ToDomain(ServerRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var server = new Server(
            new ServerId(record.Id),
            new ServerName(record.Name),
            HostAddress.Parse(record.Address),
            new Port(record.Port),
            new SshUsername(record.Username),
            new CredentialReference(record.CredentialKey, (CredentialKind)record.CredentialKind),
            MonitoringInterval.FromSeconds(record.MonitoringIntervalSeconds),
            ToJumpHost(record),
            record.GroupName is null ? null : new ServerGroup(record.GroupName),
            record.Tags.Select(tag => new ServerTag(tag.Tag)),
            record.DockerEnabled);

        // Le statut et la date de collecte ne passent pas par le constructeur : ce sont des
        // observations, pas de la configuration. Ils sont rejoués par la transition dédiée,
        // qui refuse d'ailleurs un statut « inconnu » comme conclusion.
        if (record.LastCollectedAt is { } collectedAt && (ServerStatus)record.Status != ServerStatus.Unknown)
        {
            server.RecordCollection((ServerStatus)record.Status, collectedAt);
        }

        return server;
    }

    private static JumpHost? ToJumpHost(ServerRecord record)
    {
        if (record.JumpHostAddress is null
            || record.JumpHostPort is not { } port
            || record.JumpHostUsername is null
            || record.JumpHostCredentialKey is null)
        {
            return null;
        }

        return new JumpHost(
            HostAddress.Parse(record.JumpHostAddress),
            new Port(port),
            new SshUsername(record.JumpHostUsername),
            new CredentialReference(
                record.JumpHostCredentialKey,
                (CredentialKind)(record.JumpHostCredentialKind ?? 0)));
    }
}
