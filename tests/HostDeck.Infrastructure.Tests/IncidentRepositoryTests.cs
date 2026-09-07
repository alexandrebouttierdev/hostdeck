using System;
using System.Linq;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class IncidentRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task AddThenGetActive_ReturnsOpenIncident()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        var incident = TestData.OpenIncident(server, rule);

        await Incidents.AddAsync(incident, Ct);

        var active = await Incidents.GetActiveAsync(Ct);
        var reloaded = Assert.Single(active);
        Assert.Equal(incident.Id, reloaded.Id);
        Assert.Equal(IncidentStatus.Open, reloaded.Status);
        Assert.Equal(92d, reloaded.CurrentValue);
        Assert.Equal(80d, reloaded.ThresholdValue);
    }

    [Fact]
    public async Task Update_PersistsAcknowledgement()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        var incident = TestData.OpenIncident(server, rule);
        await Incidents.AddAsync(incident, Ct);

        incident.Acknowledge(
            "alex",
            new DateTimeOffset(2026, 9, 6, 9, 10, 0, TimeSpan.Zero));
        await Incidents.UpdateAsync(incident, Ct);

        var reloaded = await Incidents.FindAsync(incident.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal(IncidentStatus.Acknowledged, reloaded!.Status);
        Assert.Equal("alex", reloaded.AcknowledgedBy);
        Assert.Equal(new DateTimeOffset(2026, 9, 6, 9, 10, 0, TimeSpan.Zero), reloaded.AcknowledgedAt);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task FindActive_ReturnsActiveThenNullAfterResolve()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        var incident = TestData.OpenIncident(server, rule);
        await Incidents.AddAsync(incident, Ct);

        Assert.NotNull(await Incidents.FindActiveAsync(server.Id, rule.Id, Ct));

        incident.Resolve(new DateTimeOffset(2026, 9, 6, 9, 30, 0, TimeSpan.Zero));
        await Incidents.UpdateAsync(incident, Ct);

        Assert.Null(await Incidents.FindActiveAsync(server.Id, rule.Id, Ct));
        Assert.Empty(await Incidents.GetActiveAsync(Ct));

        var reloaded = await Incidents.FindAsync(incident.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.Equal(IncidentStatus.Resolved, reloaded!.Status);
        Assert.Equal(new DateTimeOffset(2026, 9, 6, 9, 30, 0, TimeSpan.Zero), reloaded.ResolvedAt);
    }

    [Fact]
    public async Task ResolvedIncident_RestoresItsHistoryWithoutReplayingTransitions()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        var incident = TestData.OpenIncident(
            server,
            rule,
            startedAt: new DateTimeOffset(2026, 9, 6, 8, 0, 0, TimeSpan.Zero));
        incident.Acknowledge("alex", new DateTimeOffset(2026, 9, 6, 8, 5, 0, TimeSpan.Zero));
        incident.Recover(new DateTimeOffset(2026, 9, 6, 8, 30, 0, TimeSpan.Zero));
        incident.Resolve(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));
        await Incidents.AddAsync(incident, Ct);

        var reloaded = await Incidents.FindAsync(incident.Id, Ct);

        Assert.NotNull(reloaded);
        Assert.Equal(IncidentStatus.Resolved, reloaded!.Status);
        Assert.Equal(incident.StartedAt, reloaded.StartedAt);
        Assert.Equal(incident.ResolvedAt, reloaded.ResolvedAt);
        Assert.Equal(incident.RecoveredAt, reloaded.RecoveredAt);
        Assert.Equal(incident.LastUpdatedAt, reloaded.LastUpdatedAt);
        Assert.Equal("alex", reloaded.AcknowledgedBy);
    }

    [Fact]
    public async Task Query_JoinsServerAndRuleNames()
    {
        var server = TestData.Server(name: "web-front-01");
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule(name: "CPU élevé");
        await AlertRules.AddAsync(rule, Ct);
        await Incidents.AddAsync(TestData.OpenIncident(server, rule), Ct);

        var rows = await Incidents.QueryAsync(new IncidentFilterDto(), Ct);

        var row = Assert.Single(rows);
        Assert.Equal(server.Id.Value, row.ServerId);
        Assert.Equal("web-front-01", row.ServerName);
        Assert.Equal(MonitoredMetric.CpuUsage, row.Metric);
        Assert.False(string.IsNullOrWhiteSpace(row.Problem));
    }

    [Fact]
    public async Task Query_FiltersByServer()
    {
        var first = TestData.Server(address: "10.0.1.10", name: "web-a");
        var second = TestData.Server(address: "10.0.1.11", name: "db-b");
        await Servers.AddAsync(first, Ct);
        await Servers.AddAsync(second, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        await Incidents.AddAsync(TestData.OpenIncident(first, rule), Ct);
        await Incidents.AddAsync(TestData.OpenIncident(second, rule), Ct);

        var filtered = await Incidents.QueryAsync(new IncidentFilterDto
        {
            ServerId = first.Id.Value,
        }, Ct);

        var row = Assert.Single(filtered);
        Assert.Equal("web-a", row.ServerName);
    }

    [Fact]
    public async Task AppendEventThenTimeline_RoundTrips()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);
        var incident = TestData.OpenIncident(server, rule);
        await Incidents.AddAsync(incident, Ct);

        var occurredAt = new DateTimeOffset(2026, 9, 6, 9, 15, 0, TimeSpan.Zero);
        await Incidents.AppendEventAsync(incident.Id, new IncidentEventDto
        {
            OccurredAt = occurredAt,
            Kind = IncidentEventKind.Acknowledged,
            Detail = "Pris en charge",
            Actor = "alex",
        }, Ct);

        var timeline = await Incidents.GetTimelineAsync(incident.Id, Ct);

        var entry = Assert.Single(timeline);
        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.Equal(IncidentEventKind.Acknowledged, entry.Kind);
        Assert.Equal("Pris en charge", entry.Detail);
        Assert.Equal("alex", entry.Actor);
    }

    [Fact]
    public async Task PruneResolved_RemovesOnlyOldResolvedIncidents()
    {
        var server = TestData.Server();
        await Servers.AddAsync(server, Ct);
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);

        var oldResolved = TestData.OpenIncident(
            server,
            rule,
            startedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        oldResolved.Resolve(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));
        await Incidents.AddAsync(oldResolved, Ct);

        var recentResolved = TestData.OpenIncident(
            server,
            rule,
            startedAt: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        recentResolved.Resolve(new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero));
        await Incidents.AddAsync(recentResolved, Ct);

        var stillOpen = TestData.OpenIncident(
            server,
            rule,
            startedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        await Incidents.AddAsync(stillOpen, Ct);

        var cutoff = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        var removed = await Incidents.PruneResolvedOlderThanAsync(cutoff, Ct);

        Assert.Equal(1, removed);
        Assert.NotNull(await Incidents.FindAsync(recentResolved.Id, Ct));
        Assert.NotNull(await Incidents.FindAsync(stillOpen.Id, Ct));
        Assert.Null(await Incidents.FindAsync(oldResolved.Id, Ct));
    }
}
