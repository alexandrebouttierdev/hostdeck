using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Xunit;

namespace HostDeck.Domain.Tests.Alerts;

public sealed class ThresholdTests
{
    [Theory]
    [InlineData(ComparisonOperator.GreaterThan, 80d, 81d, true)]
    [InlineData(ComparisonOperator.GreaterThan, 80d, 80d, false)]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, 80d, 80d, true)]
    [InlineData(ComparisonOperator.LessThan, 10d, 9d, true)]
    [InlineData(ComparisonOperator.LessThan, 10d, 10d, false)]
    [InlineData(ComparisonOperator.LessThanOrEqual, 10d, 10d, true)]
    public void EvaluatesComparison(
        ComparisonOperator comparison,
        double threshold,
        double observed,
        bool expected)
    {
        var subject = new Threshold(comparison, threshold, MonitoredMetric.CpuUsage);

        Assert.Equal(expected, subject.IsBreachedBy(observed));
    }

    /// <summary>
    /// Un seuil CPU à 150 % ne peut jamais être franchi : la règle serait silencieusement
    /// inerte. Mieux vaut la refuser à la création que la laisser ne jamais se déclencher.
    /// </summary>
    [Theory]
    [InlineData(150d)]
    [InlineData(-1d)]
    public void RejectsPercentageThresholdOutOfRange(double value) =>
        Assert.Throws<DomainValidationException>(() =>
            new Threshold(ComparisonOperator.GreaterThan, value, MonitoredMetric.CpuUsage));

    [Fact]
    public void AllowsLoadAverageAboveOneHundredBecauseItIsNotAPercentage()
    {
        var threshold = new Threshold(ComparisonOperator.GreaterThan, 250d, MonitoredMetric.LoadAverage);

        Assert.True(threshold.IsBreachedBy(300d));
    }

    [Fact]
    public void RejectsNegativeNonPercentageThreshold() =>
        Assert.Throws<DomainValidationException>(() =>
            new Threshold(ComparisonOperator.GreaterThan, -1d, MonitoredMetric.LoadAverage));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsNonFiniteThreshold(double value) =>
        Assert.Throws<DomainValidationException>(() =>
            new Threshold(ComparisonOperator.GreaterThan, value, MonitoredMetric.LoadAverage));

    [Theory]
    [InlineData(MonitoredMetric.ServerUnavailable)]
    [InlineData(MonitoredMetric.GatewayUnavailable)]
    [InlineData(MonitoredMetric.DockerContainerStopped)]
    [InlineData(MonitoredMetric.DockerContainerUnhealthy)]
    public void RejectsThresholdOnStateBasedMetric(MonitoredMetric metric) =>
        Assert.Throws<DomainValidationException>(() =>
            new Threshold(ComparisonOperator.GreaterThan, 1d, metric));
}

public sealed class AlertRuleTests
{
    [Fact]
    public void RuleOnStateBasedMetricNeedsNoThreshold()
    {
        var rule = TestData.UnavailableRule();

        Assert.Null(rule.Threshold);
        Assert.True(rule.IsEnabled);
    }

    [Fact]
    public void RuleOnNumericMetricRequiresAThreshold() =>
        Assert.Throws<DomainValidationException>(() => new AlertRule(
            AlertRuleId.New(),
            "CPU sans seuil",
            MonitoredMetric.CpuUsage,
            Severity.Critical,
            AlertScope.Global));

    [Fact]
    public void RuleOnStateBasedMetricRejectsAThreshold() =>
        Assert.Throws<DomainValidationException>(() => new AlertRule(
            AlertRuleId.New(),
            "Injoignable avec seuil",
            MonitoredMetric.ServerUnavailable,
            Severity.Critical,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 1d, MonitoredMetric.LoadAverage)));

    /// <summary>
    /// Un seuil portant sur une autre métrique que la règle produirait une évaluation
    /// silencieusement fausse : la règle regarderait le CPU en comparant la charge.
    /// </summary>
    [Fact]
    public void RejectsThresholdMeasuringADifferentMetric() =>
        Assert.Throws<DomainValidationException>(() => new AlertRule(
            AlertRuleId.New(),
            "Règle incohérente",
            MonitoredMetric.CpuUsage,
            Severity.Critical,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 4d, MonitoredMetric.LoadAverage)));

    [Fact]
    public void ConditionIsMetWhenThresholdIsBreached()
    {
        var rule = TestData.CpuRule(threshold: 80d);

        Assert.True(rule.IsConditionMet(92d));
        Assert.False(rule.IsConditionMet(78d));
    }

    [Fact]
    public void FirstNotificationIsAlwaysAllowed() =>
        Assert.True(TestData.CpuRule(cooldown: TimeSpan.FromMinutes(15))
            .AllowsNotificationAt(TestData.Now, lastNotifiedAt: null));

    /// <summary>
    /// Le cooldown est ce qui empêche une tempête de notifications sur un incident déjà
    /// connu (§20).
    /// </summary>
    [Fact]
    public void NotificationIsBlockedInsideCooldown()
    {
        var rule = TestData.CpuRule(cooldown: TimeSpan.FromMinutes(15));

        Assert.False(rule.AllowsNotificationAt(
            TestData.Now.AddMinutes(14),
            TestData.Now));
    }

    [Fact]
    public void NotificationIsAllowedOnceCooldownElapsed()
    {
        var rule = TestData.CpuRule(cooldown: TimeSpan.FromMinutes(15));

        Assert.True(rule.AllowsNotificationAt(
            TestData.Now.AddMinutes(15),
            TestData.Now));
    }

    [Fact]
    public void RejectsCooldownBeyondMaximum() =>
        Assert.Throws<DomainValidationException>(() =>
            TestData.CpuRule(cooldown: TimeSpan.FromDays(8)));

    [Fact]
    public void RejectsNegativeDuration() =>
        Assert.Throws<DomainValidationException>(() =>
            TestData.CpuRule(duration: TimeSpan.FromMinutes(-1)));

    [Fact]
    public void DisableStopsTheRuleWithoutDeletingIt()
    {
        var rule = TestData.CpuRule();

        rule.Disable();

        Assert.False(rule.IsEnabled);
        Assert.True(rule.IsConditionMet(92d));
    }
}

public sealed class AlertScopeTests
{
    [Fact]
    public void GlobalScopeCoversEveryServer() =>
        Assert.True(AlertScope.Global.Covers(TestData.Server()));

    [Fact]
    public void GroupScopeCoversOnlyMembersOfThatGroup()
    {
        var databases = new ServerGroup("Databases");
        var scope = AlertScope.ForGroup(databases);

        Assert.True(scope.Covers(TestData.Server(group: databases)));
        Assert.False(scope.Covers(TestData.Server(group: new ServerGroup("Frontend"))));
        Assert.False(scope.Covers(TestData.Server()));
    }

    [Fact]
    public void GroupMatchingIsCaseInsensitive()
    {
        var scope = AlertScope.ForGroup(new ServerGroup("Databases"));

        Assert.True(scope.Covers(TestData.Server(group: new ServerGroup("databases"))));
    }

    [Fact]
    public void ServerScopeCoversOnlyThatServer()
    {
        var server = TestData.Server();
        var scope = AlertScope.ForServer(server.Id);

        Assert.True(scope.Covers(server));
        Assert.False(scope.Covers(TestData.Server()));
    }

    /// <summary>
    /// La spécificité arbitre les règles concurrentes : sans elle, une règle globale
    /// écraserait une exception posée volontairement sur un serveur (§21).
    /// </summary>
    [Fact]
    public void SpecificityRanksServerAboveGroupAboveGlobal()
    {
        var server = AlertScope.ForServer(ServerId.New()).Specificity;
        var group = AlertScope.ForGroup(new ServerGroup("Databases")).Specificity;
        var global = AlertScope.Global.Specificity;

        Assert.True(server > group);
        Assert.True(group > global);
    }
}
