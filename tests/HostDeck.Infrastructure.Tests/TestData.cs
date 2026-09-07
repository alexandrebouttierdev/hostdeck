using System;
using System.Collections.Generic;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Infrastructure.Tests;

/// <summary>
/// Fabriques d'objets valides pour les tests d'Infrastructure.
///
/// Chaque fabrique produit une instance correcte que le test n'a plus qu'à dévier sur le
/// seul point qu'il vérifie, ce qui évite de reconstruire un agrégat entier à chaque
/// assertion.
/// </summary>
internal static class TestData
{
    /// <summary>Instant zéro des horodatages de métriques : les buckets sont entiers.</summary>
    public static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    public static Server Server(
        string address = "10.0.1.10",
        string name = "web-front-01",
        ServerGroup? group = null,
        JumpHost? jumpHost = null)
    {
        var id = ServerId.New();
        return new Server(
            id,
            new ServerName(name),
            HostAddress.Parse(address),
            new Port(22),
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default,
            jumpHost,
            group);
    }

    public static JumpHost JumpHost(string address = "10.0.0.1") =>
        new(
            HostAddress.Parse(address),
            new Port(2222),
            new SshUsername("bastion"),
            new CredentialReference("hostdeck:bastion:1", CredentialKind.PrivateKey));

    public static AlertRule CpuRule(
        string name = "CPU élevé",
        double threshold = 80d,
        Severity severity = Severity.Critical,
        AlertScope? scope = null,
        bool enabled = true) =>
        new(
            AlertRuleId.New(),
            name,
            MonitoredMetric.CpuUsage,
            severity,
            scope ?? AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, threshold, MonitoredMetric.CpuUsage),
            null,
            null,
            description: null,
            enabled: enabled);

    public static AlertRule UnavailableRule(
        string name = "Serveur injoignable",
        Severity severity = Severity.Critical,
        bool enabled = true) =>
        new(
            AlertRuleId.New(),
            name,
            MonitoredMetric.ServerUnavailable,
            severity,
            AlertScope.Global,
            threshold: null,
            duration: null,
            cooldown: null,
            description: null,
            enabled: enabled);

    public static Incident OpenIncident(
        Server server,
        AlertRule rule,
        DateTimeOffset? startedAt = null,
        double? triggeringValue = 92d) =>
        Incident.Open(
            IncidentId.New(),
            server.Id,
            rule,
            startedAt ?? new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero),
            triggeringValue);

    /// <summary>
    /// Relevé de métriques à un instant donné, avec une charge CPU et mémoire contrôlées.
    /// </summary>
    public static MetricSample Sample(
        ServerId serverId,
        long elapsedMinutes,
        double cpuTotal = 30.1,
        long memoryUsedMib = 11800,
        IEnumerable<DiskUsage>? disks = null,
        IEnumerable<NetworkUsage>? interfaces = null)
    {
        var cpu = new CpuUsage(
            new Percentage(18.4),
            new Percentage(7.2),
            new Percentage(4.1),
            new Percentage(0.3),
            new Percentage(0.1),
            4);

        return new MetricSample(
            serverId,
            Epoch.AddMinutes(elapsedMinutes),
            cpuTotal == 30.1 ? cpu : CpuWithTotal(cpuTotal),
            new MemoryUsage(
                ByteSize.FromMebibytes(20480),
                ByteSize.FromMebibytes(memoryUsedMib),
                ByteSize.FromMebibytes(4300),
                ByteSize.FromMebibytes(2150)),
            SwapUsage.None,
            new LoadAverage(2.91, 2.14, 1.47),
            Uptime.FromSeconds(3600 + elapsedMinutes * 60),
            disks,
            interfaces);
    }

    public static DiskUsage Disk(string mountPoint = "/", long totalMib = 100_000, long usedMib = 40_000) =>
        new(
            mountPoint,
            "ext4",
            ByteSize.FromMebibytes(totalMib),
            ByteSize.FromMebibytes(usedMib));

    public static NetworkUsage Interface(
        string name = "eth0",
        long receivedBytes = 1_000_000,
        long transmittedBytes = 500_000) =>
        new(name, new ByteSize(receivedBytes), new ByteSize(transmittedBytes));

    /// <summary>
    /// CPU dont la somme des modes vaut exactement <paramref name="total"/> : le user porte
    /// la différence, ce qui permet aux tests d'agrégation de prévoir le total.
    /// </summary>
    private static CpuUsage CpuWithTotal(double total)
    {
        var fixedModes = 7.2 + 4.1 + 0.3 + 0.1;
        var user = total - fixedModes;

        return new CpuUsage(
            new Percentage(user),
            new Percentage(7.2),
            new Percentage(4.1),
            new Percentage(0.3),
            new Percentage(0.1),
            4);
    }
}
