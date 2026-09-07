using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HostDeck.Application.Dtos.Incidents;
using HostDeck.Application.Errors;
using HostDeck.Application.Incidents;
using HostDeck.Application.Monitoring.Events;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostDeck.Application.Tests.Incidents;

public sealed class AcknowledgeIncidentUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 10, 24, 15, TimeSpan.Zero);

    private readonly FakeIncidentRepository _incidents = new();
    private readonly RecordingEventBus _events = new();
    private readonly FakeClock _clock = new(Now);
    private readonly AcknowledgeIncidentUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AcknowledgeIncidentUseCaseTests()
    {
        _useCase = new AcknowledgeIncidentUseCase(
            _incidents,
            _events,
            _clock,
            NullLogger<AcknowledgeIncidentUseCase>.Instance);
    }

    private static AlertRule CpuRule() => new(
        AlertRuleId.New(),
        "CPU élevé sur serveur",
        MonitoredMetric.CpuUsage,
        Severity.Critical,
        AlertScope.Global,
        new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));

    private Incident SeedOpenIncident()
    {
        var incident = Incident.Open(IncidentId.New(), ServerId.New(), CpuRule(), Now, 92d);
        _incidents.Seed(incident);
        return incident;
    }

    [Fact]
    public async Task AcknowledgesTheIncident()
    {
        var incident = SeedOpenIncident();
        _clock.Advance(TimeSpan.FromMinutes(5));

        await _useCase.ExecuteAsync(
            new AcknowledgeIncidentDto { IncidentId = incident.Id.Value, AcknowledgedBy = "ops-team" },
            Ct);

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
        Assert.Equal("ops-team", incident.AcknowledgedBy);
        Assert.Equal(Now.AddMinutes(5), incident.AcknowledgedAt);
    }

    [Fact]
    public async Task AppendsAnEntryToTheTimeline()
    {
        var incident = SeedOpenIncident();

        await _useCase.ExecuteAsync(
            new AcknowledgeIncidentDto
            {
                IncidentId = incident.Id.Value,
                AcknowledgedBy = "ops-team",
                Note = "Pris en charge",
            },
            Ct);

        var timeline = await _incidents.GetTimelineAsync(incident.Id, Ct);
        var entry = Assert.Single(timeline);

        Assert.Equal(IncidentEventKind.Acknowledged, entry.Kind);
        Assert.Equal("ops-team", entry.Actor);
        Assert.Equal("Pris en charge", entry.Detail);
    }

    /// <summary>
    /// L'interface doit se rafraîchir sur cet acquittement, ce qui passe par le bus et non
    /// par une référence directe du use case sur un ViewModel (§64).
    /// </summary>
    [Fact]
    public async Task PublishesAChangeEvent()
    {
        var incident = SeedOpenIncident();

        await _useCase.ExecuteAsync(
            new AcknowledgeIncidentDto { IncidentId = incident.Id.Value, AcknowledgedBy = "ops-team" },
            Ct);

        var published = Assert.Single(_events.Published);
        var changed = Assert.IsType<IncidentChangedEvent>(published);

        Assert.Equal(IncidentChangeKind.Acknowledged, changed.Change);
        Assert.Equal(IncidentStatus.Acknowledged, changed.Status);
    }

    [Fact]
    public async Task FailsWhenTheIncidentDoesNotExist() =>
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _useCase.ExecuteAsync(
            new AcknowledgeIncidentDto { IncidentId = Guid.NewGuid(), AcknowledgedBy = "ops-team" },
            Ct));

    /// <summary>
    /// La règle est tenue par le domaine, pas par le use case : ce test vérifie que le use
    /// case ne la contourne pas.
    /// </summary>
    [Fact]
    public async Task CannotAcknowledgeAResolvedIncident()
    {
        var incident = SeedOpenIncident();
        incident.Resolve(Now);

        await Assert.ThrowsAsync<DomainValidationException>(() => _useCase.ExecuteAsync(
            new AcknowledgeIncidentDto { IncidentId = incident.Id.Value, AcknowledgedBy = "ops-team" },
            Ct));
    }

    [Fact]
    public async Task CannotAcknowledgeTwice()
    {
        var incident = SeedOpenIncident();
        var request = new AcknowledgeIncidentDto
        {
            IncidentId = incident.Id.Value,
            AcknowledgedBy = "ops-team",
        };

        await _useCase.ExecuteAsync(request, Ct);

        await Assert.ThrowsAsync<DomainValidationException>(() => _useCase.ExecuteAsync(request, Ct));
    }
}

public sealed class ResolveIncidentUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 10, 24, 15, TimeSpan.Zero);

    private readonly FakeIncidentRepository _incidents = new();
    private readonly RecordingEventBus _events = new();
    private readonly FakeClock _clock = new(Now);
    private readonly ResolveIncidentUseCase _useCase;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ResolveIncidentUseCaseTests()
    {
        _useCase = new ResolveIncidentUseCase(
            _incidents,
            _events,
            _clock,
            NullLogger<ResolveIncidentUseCase>.Instance);
    }

    private Incident SeedOpenIncident()
    {
        var rule = new AlertRule(
            AlertRuleId.New(),
            "Serveur injoignable",
            MonitoredMetric.ServerUnavailable,
            Severity.Critical,
            AlertScope.Global);

        var incident = Incident.Open(IncidentId.New(), ServerId.New(), rule, Now);
        _incidents.Seed(incident);
        return incident;
    }

    [Fact]
    public async Task ResolvesTheIncident()
    {
        var incident = SeedOpenIncident();
        _clock.Advance(TimeSpan.FromMinutes(30));

        await _useCase.ExecuteAsync(incident.Id.Value, Ct);

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.False(incident.IsActive);
        Assert.Equal(TimeSpan.FromMinutes(30), incident.DurationAt(Now.AddHours(5)));
    }

    [Fact]
    public async Task IsTerminal()
    {
        var incident = SeedOpenIncident();
        await _useCase.ExecuteAsync(incident.Id.Value, Ct);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _useCase.ExecuteAsync(incident.Id.Value, Ct));
    }

    [Fact]
    public async Task FailsWhenTheIncidentDoesNotExist() =>
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _useCase.ExecuteAsync(Guid.NewGuid(), Ct));
}

public sealed class IncidentMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 10, 24, 15, TimeSpan.Zero);

    [Fact]
    public void BuildsAReadableProblemLabelFromTheRule()
    {
        var rule = new AlertRule(
            AlertRuleId.New(),
            "CPU élevé",
            MonitoredMetric.CpuUsage,
            Severity.Critical,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));

        var incident = Incident.Open(IncidentId.New(), ServerId.New(), rule, Now, 92d);

        Assert.Equal("Utilisation CPU > 80 %", IncidentMapper.DescribeProblem(incident, rule));
    }

    [Fact]
    public void StateBasedMetricNeedsNoThresholdInItsLabel()
    {
        var rule = new AlertRule(
            AlertRuleId.New(),
            "Injoignable",
            MonitoredMetric.ServerUnavailable,
            Severity.Critical,
            AlertScope.Global);

        var incident = Incident.Open(IncidentId.New(), ServerId.New(), rule, Now);

        Assert.Equal("Serveur injoignable", IncidentMapper.DescribeProblem(incident, rule));
    }

    /// <summary>
    /// Un incident dont la règle ou le serveur a disparu doit rester lisible : son historique
    /// ne doit pas s'effacer avec eux.
    /// </summary>
    [Fact]
    public void SurvivesADeletedRuleAndServer()
    {
        var rule = new AlertRule(
            AlertRuleId.New(),
            "CPU élevé",
            MonitoredMetric.CpuUsage,
            Severity.High,
            AlertScope.Global,
            new Threshold(ComparisonOperator.GreaterThan, 80d, MonitoredMetric.CpuUsage));

        var incident = Incident.Open(IncidentId.New(), ServerId.New(), rule, Now, 92d);

        var dto = IncidentMapper.ToDto(incident, serverName: null, rule: null, Now);

        Assert.Equal("Serveur supprimé", dto.ServerName);
        Assert.Equal(Severity.High, dto.Severity);
        Assert.Equal(80d, dto.ThresholdValue);
    }
}
