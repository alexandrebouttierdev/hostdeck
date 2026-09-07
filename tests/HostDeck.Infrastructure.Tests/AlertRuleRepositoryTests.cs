using System.Threading.Tasks;
using HostDeck.Application.Errors;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class AlertRuleRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task AddThenGetAll_RoundTripsThresholdRule()
    {
        var rule = TestData.CpuRule(
            name: "CPU élevé",
            threshold: 85d,
            severity: Severity.Warning);

        await AlertRules.AddAsync(rule, Ct);

        var reloaded = Assert.Single(await AlertRules.GetAllAsync(Ct));

        Assert.Equal(rule.Id, reloaded.Id);
        Assert.Equal("CPU élevé", reloaded.Name);
        Assert.Equal(MonitoredMetric.CpuUsage, reloaded.Metric);
        Assert.Equal(Severity.Warning, reloaded.Severity);
        Assert.Equal(AlertScopeKind.Global, reloaded.Scope.Kind);
        Assert.NotNull(reloaded.Threshold);
        Assert.Equal(ComparisonOperator.GreaterThan, reloaded.Threshold!.Comparison);
        Assert.Equal(85d, reloaded.Threshold.Value);
        Assert.True(reloaded.IsEnabled);
    }

    [Fact]
    public async Task AddThenGetAll_StateRuleHasNoThreshold()
    {
        var rule = TestData.UnavailableRule();

        await AlertRules.AddAsync(rule, Ct);

        var reloaded = Assert.Single(await AlertRules.GetAllAsync(Ct));

        Assert.Equal(MonitoredMetric.ServerUnavailable, reloaded.Metric);
        Assert.Null(reloaded.Threshold);
    }

    [Fact]
    public async Task GetEnabled_FiltersDisabledRules()
    {
        var enabled = TestData.CpuRule(name: "Active");
        var disabled = TestData.CpuRule(name: "Désactivée");
        disabled.Disable();
        await AlertRules.AddAsync(enabled, Ct);
        await AlertRules.AddAsync(disabled, Ct);

        var active = await AlertRules.GetEnabledAsync(Ct);

        var rule = Assert.Single(active);
        Assert.Equal("Active", rule.Name);
    }

    [Fact]
    public async Task Find_ReturnsNullForUnknownRule()
    {
        Assert.Null(await AlertRules.FindAsync(AlertRuleId.New(), Ct));
    }

    [Fact]
    public async Task Update_PersistsThresholdAndDisable()
    {
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);

        rule.ChangeThreshold(new Threshold(
            ComparisonOperator.LessThan,
            20d,
            MonitoredMetric.CpuUsage));
        rule.Disable();
        await AlertRules.UpdateAsync(rule, Ct);

        var reloaded = await AlertRules.FindAsync(rule.Id, Ct);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsEnabled);
        Assert.NotNull(reloaded.Threshold);
        Assert.Equal(ComparisonOperator.LessThan, reloaded.Threshold!.Comparison);
        Assert.Equal(20d, reloaded.Threshold.Value);
    }

    [Fact]
    public async Task Delete_RemovesRule()
    {
        var rule = TestData.CpuRule();
        await AlertRules.AddAsync(rule, Ct);

        await AlertRules.DeleteAsync(rule.Id, Ct);

        Assert.Empty(await AlertRules.GetAllAsync(Ct));
    }

    [Fact]
    public async Task Delete_OnUnknownRule_Throws()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => AlertRules.DeleteAsync(AlertRuleId.New(), Ct));
    }
}
