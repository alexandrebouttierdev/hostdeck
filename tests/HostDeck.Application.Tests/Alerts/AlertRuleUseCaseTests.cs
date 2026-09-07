using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Alerts;
using HostDeck.Application.Alerts.Validation;
using HostDeck.Application.Dtos.Alerts;
using HostDeck.Application.Errors;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
// FluentValidation expose son propre type Severity : l'alias lève l'ambiguïté
// sans masquer laquelle des deux est utilisée.
using Severity = HostDeck.Domain.Incidents.Severity;

namespace HostDeck.Application.Tests.Alerts;

public sealed class CreateAlertRuleUseCaseTests
{
    private readonly FakeAlertRuleRepository _rules = new();
    private readonly CreateAlertRuleUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CreateAlertRuleUseCaseTests()
    {
        _useCase = new CreateAlertRuleUseCase(
            _rules,
            new CreateAlertRuleDtoValidator(),
            NullLogger<CreateAlertRuleUseCase>.Instance);
    }

    private static CreateAlertRuleDto CpuRequest(double threshold = 80d) => new()
    {
        Name = "CPU élevé sur serveur",
        Metric = MonitoredMetric.CpuUsage,
        Severity = Severity.Critical,
        Comparison = ComparisonOperator.GreaterThan,
        ThresholdValue = threshold,
        Duration = TimeSpan.FromMinutes(5),
        Cooldown = TimeSpan.FromMinutes(15),
    };

    [Fact]
    public async Task CreatesANumericRule()
    {
        var dto = await _useCase.ExecuteAsync(CpuRequest(), Ct);

        Assert.Equal(MonitoredMetric.CpuUsage, dto.Metric);
        Assert.Equal(80d, dto.ThresholdValue);
        Assert.Equal(ComparisonOperator.GreaterThan, dto.Comparison);
        Assert.True(dto.IsEnabled);
    }

    [Fact]
    public async Task CreatesAStateBasedRuleWithoutAThreshold()
    {
        var request = new CreateAlertRuleDto
        {
            Name = "Serveur injoignable",
            Metric = MonitoredMetric.ServerUnavailable,
            Severity = Severity.Critical,
        };

        var dto = await _useCase.ExecuteAsync(request, Ct);

        Assert.Null(dto.ThresholdValue);
        Assert.Null(dto.Comparison);
    }

    /// <summary>
    /// Un seuil CPU à 150 % ne serait jamais franchi : la règle serait silencieusement inerte.
    /// </summary>
    [Theory]
    [InlineData(150d)]
    [InlineData(-1d)]
    public async Task RejectsAPercentageThresholdOutOfRange(double threshold) =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _useCase.ExecuteAsync(CpuRequest(threshold), Ct));

    [Fact]
    public async Task RejectsANumericRuleWithoutAThreshold()
    {
        var request = CpuRequest() with { ThresholdValue = null };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsAThresholdOnAStateBasedMetric()
    {
        var request = new CreateAlertRuleDto
        {
            Name = "Injoignable avec seuil",
            Metric = MonitoredMetric.ServerUnavailable,
            Severity = Severity.Critical,
            Comparison = ComparisonOperator.GreaterThan,
            ThresholdValue = 1d,
        };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsACooldownBeyondMaximum()
    {
        var request = CpuRequest() with { Cooldown = TimeSpan.FromDays(8) };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsAGroupScopeWithoutAGroup()
    {
        var request = CpuRequest() with { ScopeKind = AlertScopeKind.Group, ScopeGroup = null };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsAServerScopeWithoutAServer()
    {
        var request = CpuRequest() with { ScopeKind = AlertScopeKind.Server, ScopeServerId = null };

        await Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task AcceptsAGroupScope()
    {
        var request = CpuRequest() with
        {
            ScopeKind = AlertScopeKind.Group,
            ScopeGroup = "web-front",
        };

        var dto = await _useCase.ExecuteAsync(request, Ct);

        Assert.Equal(AlertScopeKind.Group, dto.ScopeKind);
        Assert.Equal("web-front", dto.ScopeGroup);
    }

    [Fact]
    public async Task AllowsALoadThresholdAboveOneHundred()
    {
        var request = new CreateAlertRuleDto
        {
            Name = "Charge très élevée",
            Metric = MonitoredMetric.LoadAverage,
            Severity = Severity.High,
            Comparison = ComparisonOperator.GreaterThan,
            ThresholdValue = 250d,
        };

        var dto = await _useCase.ExecuteAsync(request, Ct);

        Assert.Equal(250d, dto.ThresholdValue);
    }
}

public sealed class UpdateAlertRuleUseCaseTests
{
    private readonly FakeAlertRuleRepository _rules = new();
    private readonly UpdateAlertRuleUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UpdateAlertRuleUseCaseTests()
    {
        _useCase = new UpdateAlertRuleUseCase(
            _rules,
            new UpdateAlertRuleDtoValidator(),
            NullLogger<UpdateAlertRuleUseCase>.Instance);
    }

    private AlertRule SeedCpuRule()
    {
        var rule = new AlertRule(
            AlertRuleId.New(),
            "CPU élevé",
            MonitoredMetric.CpuUsage,
            Severity.Warning,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));

        _rules.Seed(rule);
        return rule;
    }

    private static UpdateAlertRuleDto Request(AlertRule rule) => new()
    {
        RuleId = rule.Id.Value,
        Name = "CPU critique",
        Severity = Severity.Critical,
        Comparison = ComparisonOperator.GreaterThan,
        ThresholdValue = 90d,
        Duration = TimeSpan.FromMinutes(5),
        Cooldown = TimeSpan.FromMinutes(15),
    };

    [Fact]
    public async Task UpdatesTheRule()
    {
        var rule = SeedCpuRule();

        var dto = await _useCase.ExecuteAsync(Request(rule), Ct);

        Assert.Equal("CPU critique", dto.Name);
        Assert.Equal(Severity.Critical, dto.Severity);
        Assert.Equal(90d, dto.ThresholdValue);
    }

    /// <summary>
    /// La métrique d'une règle n'est pas modifiable : changer la grandeur surveillée
    /// désynchroniserait l'historique des incidents déjà ouverts par cette règle.
    /// </summary>
    [Fact]
    public async Task KeepsTheOriginalMetric()
    {
        var rule = SeedCpuRule();

        var dto = await _useCase.ExecuteAsync(Request(rule), Ct);

        Assert.Equal(MonitoredMetric.CpuUsage, dto.Metric);
    }

    [Fact]
    public async Task CanDisableARuleWithoutDeletingIt()
    {
        var rule = SeedCpuRule();
        var request = Request(rule) with { IsEnabled = false };

        var dto = await _useCase.ExecuteAsync(request, Ct);

        Assert.False(dto.IsEnabled);
        Assert.NotNull(await _rules.FindAsync(rule.Id, Ct));
    }

    [Fact]
    public async Task FailsWhenTheRuleDoesNotExist()
    {
        var absent = new AlertRule(
            AlertRuleId.New(),
            "Absente",
            MonitoredMetric.CpuUsage,
            Severity.Warning,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _useCase.ExecuteAsync(Request(absent), Ct));
    }
}

public sealed class DeleteAlertRuleUseCaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeletesTheRule()
    {
        var rules = new FakeAlertRuleRepository();
        var rule = new AlertRule(
            AlertRuleId.New(),
            "CPU élevé",
            MonitoredMetric.CpuUsage,
            Severity.Warning,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));
        rules.Seed(rule);

        var useCase = new DeleteAlertRuleUseCase(rules, NullLogger<DeleteAlertRuleUseCase>.Instance);

        await useCase.ExecuteAsync(rule.Id.Value, Ct);

        Assert.Null(await rules.FindAsync(rule.Id, Ct));
    }

    [Fact]
    public async Task FailsWhenTheRuleDoesNotExist()
    {
        var useCase = new DeleteAlertRuleUseCase(
            new FakeAlertRuleRepository(),
            NullLogger<DeleteAlertRuleUseCase>.Instance);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), Ct));
    }
}
