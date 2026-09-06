using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Servers;

/// <summary>
/// Serveur supervisé par HostDeck. Agrégat racine du module Servers.
///
/// L'état de supervision (<see cref="Status"/>, <see cref="LastCollectedAt"/>) est porté par
/// l'agrégat et ne change que par les transitions explicites définies plus bas : rien ne peut
/// écrire un statut arbitraire.
/// </summary>
public sealed class Server
{
    private readonly HashSet<ServerTag> _tags;

    public Server(
        ServerId id,
        ServerName name,
        HostAddress address,
        Port port,
        SshUsername username,
        CredentialReference credential,
        MonitoringInterval monitoringInterval,
        JumpHost? jumpHost = null,
        ServerGroup? group = null,
        IEnumerable<ServerTag>? tags = null,
        bool dockerEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(credential);

        EnsureJumpHostIsNotTheTarget(address, port, jumpHost);

        Id = id;
        Name = name;
        Address = address;
        Port = port;
        Username = username;
        Credential = credential;
        MonitoringInterval = monitoringInterval;
        JumpHost = jumpHost;
        Group = group;
        DockerEnabled = dockerEnabled;
        _tags = tags is null ? [] : [.. tags];
        Status = ServerStatus.Unknown;
    }

    public ServerId Id { get; }

    public ServerName Name { get; private set; }

    public HostAddress Address { get; private set; }

    public Port Port { get; private set; }

    public SshUsername Username { get; private set; }

    public CredentialReference Credential { get; private set; }

    public MonitoringInterval MonitoringInterval { get; private set; }

    /// <summary>Bastion traversé, ou <c>null</c> pour une connexion directe.</summary>
    public JumpHost? JumpHost { get; private set; }

    public ServerGroup? Group { get; private set; }

    public bool DockerEnabled { get; private set; }

    public ServerStatus Status { get; private set; }

    /// <summary>Horodatage UTC de la dernière tentative de collecte, réussie ou non.</summary>
    public DateTimeOffset? LastCollectedAt { get; private set; }

    /// <summary>
    /// Mode de connexion déduit de la configuration. La collecte n'a pas à connaître ce mode :
    /// il n'existe que pour l'affichage et le diagnostic (§12).
    /// </summary>
    public ConnectionMode ConnectionMode =>
        JumpHost is null ? ConnectionMode.Direct : ConnectionMode.JumpHost;

    public ImmutableArray<ServerTag> Tags => [.. _tags];

    public void Rename(ServerName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public void ChangeEndpoint(HostAddress address, Port port, SshUsername username)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(username);

        EnsureJumpHostIsNotTheTarget(address, port, JumpHost);

        Address = address;
        Port = port;
        Username = username;
    }

    public void ChangeCredential(CredentialReference credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        Credential = credential;
    }

    public void ChangeMonitoringInterval(MonitoringInterval interval) => MonitoringInterval = interval;

    public void MoveToGroup(ServerGroup? group) => Group = group;

    public void SetDockerEnabled(bool enabled) => DockerEnabled = enabled;

    /// <summary>
    /// Fait passer le serveur en connexion directe ou par bastion.
    /// </summary>
    public void ChangeJumpHost(JumpHost? jumpHost)
    {
        EnsureJumpHostIsNotTheTarget(Address, Port, jumpHost);
        JumpHost = jumpHost;
    }

    public bool AddTag(ServerTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return _tags.Add(tag);
    }

    public bool RemoveTag(ServerTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return _tags.Remove(tag);
    }

    public void ReplaceTags(IEnumerable<ServerTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        _tags.Clear();
        foreach (var tag in tags)
        {
            _tags.Add(tag);
        }
    }

    public bool HasTag(ServerTag tag) => _tags.Contains(tag);

    /// <summary>
    /// Enregistre l'issue d'un cycle de collecte.
    /// </summary>
    /// <param name="status">Statut observé. <see cref="ServerStatus.Unknown"/> est refusé :
    /// une collecte aboutie produit toujours une conclusion.</param>
    /// <param name="observedAt">Horodatage UTC de l'observation.</param>
    public void RecordCollection(ServerStatus status, DateTimeOffset observedAt)
    {
        if (status == ServerStatus.Unknown)
        {
            throw new DomainValidationException(
                nameof(status),
                "une collecte ne peut pas conclure sur un statut inconnu");
        }

        Guard.RequiredInstant(observedAt);

        if (LastCollectedAt is { } previous)
        {
            Guard.NotBefore(observedAt, previous, nameof(observedAt), nameof(LastCollectedAt));
        }

        Status = status;
        LastCollectedAt = observedAt.ToUniversalTime();
    }

    /// <summary>
    /// Un bastion qui pointe sur sa propre cible ne fournit aucun chemin : la connexion
    /// tournerait en boucle sur elle-même. C'est le cas de cycle que le §41 impose de tester.
    /// </summary>
    private static void EnsureJumpHostIsNotTheTarget(
        HostAddress address,
        Port port,
        JumpHost? jumpHost)
    {
        if (jumpHost is null)
        {
            return;
        }

        var sameEndpoint = jumpHost.Port == port
            && string.Equals(jumpHost.Address.Value, address.Value, StringComparison.OrdinalIgnoreCase);

        if (sameEndpoint)
        {
            throw new DomainValidationException(
                nameof(jumpHost),
                "le bastion ne peut pas être la cible elle-même");
        }
    }
}
