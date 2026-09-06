using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;

namespace HostDeck.Domain.Tests;

/// <summary>
/// Constructeurs d'objets valides pour les tests. Chaque fabrique produit une instance
/// correcte que le test n'a plus qu'à dévier sur le seul point qu'il vérifie, ce qui évite
/// de reconstruire un agrégat entier à chaque assertion.
/// </summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now =
        new(2026, 9, 6, 10, 24, 15, TimeSpan.Zero);

    public static Server Server(
        JumpHost? jumpHost = null,
        ServerGroup? group = null,
        string address = "10.0.1.10",
        int port = 22)
    {
        var id = ServerId.New();
        return new Server(
            id,
            new ServerName("web-front-01"),
            HostAddress.Parse(address),
            new Port(port),
            new SshUsername("hostdeck"),
            CredentialReference.ForServer(id, CredentialKind.PrivateKey),
            MonitoringInterval.Default,
            jumpHost,
            group);
    }

    public static JumpHost JumpHost(string address = "10.0.0.1", int port = 22) =>
        new(
            HostAddress.Parse(address),
            new Port(port),
            new SshUsername("bastion"),
            new CredentialReference("hostdeck:bastion:1", CredentialKind.PrivateKey));

    public static AlertRule CpuRule(
        double threshold = 80d,
        Severity severity = Severity.Critical,
        TimeSpan? duration = null,
        TimeSpan? cooldown = null) =>
        new(
            AlertRuleId.New(),
            "CPU élevé sur serveur",
            MonitoredMetric.CpuUsage,
            severity,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, threshold, MonitoredMetric.CpuUsage),
            duration,
            cooldown);

    public static AlertRule UnavailableRule(Severity severity = Severity.Critical) =>
        new(
            AlertRuleId.New(),
            "Serveur injoignable",
            MonitoredMetric.ServerUnavailable,
            severity,
            AlertScope.Global);

    public static CpuUsage Cpu(double user = 18.4, double system = 7.2, int cores = 4) =>
        new(
            new Percentage(user),
            new Percentage(system),
            new Percentage(4.1),
            new Percentage(0.3),
            new Percentage(0.1),
            cores);

    public static MemoryUsage Memory(long totalMib = 20480, long usedMib = 11800) =>
        new(
            ByteSize.FromMebibytes(totalMib),
            ByteSize.FromMebibytes(usedMib),
            ByteSize.FromMebibytes(4300),
            ByteSize.FromMebibytes(2150));

    public static MetricSample Sample(DateTimeOffset? observedAt = null, double uptimeSeconds = 3600d) =>
        new(
            ServerId.New(),
            observedAt ?? Now,
            Cpu(),
            Memory(),
            SwapUsage.None,
            new LoadAverage(2.91, 2.14, 1.47),
            Uptime.FromSeconds(uptimeSeconds));
}
