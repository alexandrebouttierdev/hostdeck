using System;
using HostDeck.Domain.Alerts;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Servers;
using HostDeck.Domain.Shared;
using Xunit;

namespace HostDeck.Domain.Tests.Incidents;

public sealed class IncidentTests
{
    private static Incident OpenIncident(double? value = 92d) => Incident.Open(
        IncidentId.New(),
        ServerId.New(),
        TestData.CpuRule(),
        TestData.Now,
        value);

    [Fact]
    public void NewIncidentIsOpenAndActive()
    {
        var incident = OpenIncident();

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.True(incident.IsActive);
        Assert.Null(incident.AcknowledgedAt);
        Assert.Null(incident.RecoveredAt);
        Assert.Null(incident.ResolvedAt);
    }

    [Fact]
    public void OpenCopiesSeverityMetricAndThresholdFromTheRule()
    {
        var rule = TestData.CpuRule(threshold: 85d, severity: Severity.High);

        var incident = Incident.Open(IncidentId.New(), ServerId.New(), rule, TestData.Now, 91d);

        Assert.Equal(rule.Id, incident.RuleId);
        Assert.Equal(rule.Metric, incident.Metric);
        Assert.Equal(Severity.High, incident.Severity);
        Assert.Equal(85d, incident.ThresholdValue);
        Assert.Equal(91d, incident.CurrentValue);
    }

    [Fact]
    public void AcknowledgeRecordsWhoAndWhen()
    {
        var incident = OpenIncident();
        var at = TestData.Now.AddMinutes(5);

        incident.Acknowledge("ops-team", at);

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
        Assert.Equal("ops-team", incident.AcknowledgedBy);
        Assert.Equal(at, incident.AcknowledgedAt);
        Assert.True(incident.IsActive);
    }

    [Fact]
    public void AcknowledgeTwiceIsRejected()
    {
        var incident = OpenIncident();
        incident.Acknowledge("ops-team", TestData.Now.AddMinutes(1));

        Assert.Throws<DomainValidationException>(() =>
            incident.Acknowledge("secops", TestData.Now.AddMinutes(2)));
    }

    [Fact]
    public void AcknowledgeRejectsAnEmptyOperator() =>
        Assert.Throws<DomainValidationException>(() =>
            OpenIncident().Acknowledge("  ", TestData.Now.AddMinutes(1)));

    /// <summary>
    /// Un incident rétabli sans avoir été acquitté reste visible : c'est exactement le cas
    /// d'une panne que personne n'a constatée, qu'il ne faut pas faire disparaître.
    /// </summary>
    [Fact]
    public void RecoverKeepsTheIncidentButMarksItInactive()
    {
        var incident = OpenIncident();
        var at = TestData.Now.AddMinutes(10);

        incident.Recover(at);

        Assert.Equal(IncidentStatus.Recovered, incident.Status);
        Assert.Equal(at, incident.RecoveredAt);
        Assert.False(incident.IsActive);
        Assert.Null(incident.ResolvedAt);
    }

    [Fact]
    public void RecoverIsIdempotent()
    {
        var incident = OpenIncident();
        incident.Recover(TestData.Now.AddMinutes(10));

        incident.Recover(TestData.Now.AddMinutes(20));

        Assert.Equal(TestData.Now.AddMinutes(10), incident.RecoveredAt);
    }

    /// <summary>
    /// Rebond de la condition après un rétablissement : l'incident redevient actif plutôt
    /// que de rester faussement rétabli.
    /// </summary>
    [Fact]
    public void ObservationAfterRecoveryReopensTheIncident()
    {
        var incident = OpenIncident();
        incident.Recover(TestData.Now.AddMinutes(10));

        incident.RecordObservation(95d, TestData.Now.AddMinutes(11));

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Null(incident.RecoveredAt);
        Assert.Equal(95d, incident.CurrentValue);
    }

    [Fact]
    public void ObservationDoesNotUndoAnAcknowledgement()
    {
        var incident = OpenIncident();
        incident.Acknowledge("ops-team", TestData.Now.AddMinutes(1));

        incident.RecordObservation(97d, TestData.Now.AddMinutes(2));

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
        Assert.Equal("ops-team", incident.AcknowledgedBy);
    }

    [Fact]
    public void ResolveIsTerminal()
    {
        var incident = OpenIncident();
        incident.Resolve(TestData.Now.AddMinutes(30));

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.False(incident.IsActive);
    }

    [Theory]
    [InlineData("acknowledge")]
    [InlineData("recover")]
    [InlineData("resolve")]
    [InlineData("observe")]
    [InlineData("escalate")]
    public void NoTransitionIsAllowedAfterResolution(string operation)
    {
        var incident = OpenIncident();
        var resolvedAt = TestData.Now.AddMinutes(30);
        incident.Resolve(resolvedAt);
        var later = resolvedAt.AddMinutes(1);

        Action act = operation switch
        {
            "acknowledge" => () => incident.Acknowledge("ops-team", later),
            "recover" => () => incident.Recover(later),
            "resolve" => () => incident.Resolve(later),
            "observe" => () => incident.RecordObservation(50d, later),
            _ => () => incident.Escalate(Severity.Critical, later),
        };

        Assert.Throws<DomainValidationException>(act);
    }

    [Fact]
    public void EscalationRaisesSeverity()
    {
        var incident = Incident.Open(
            IncidentId.New(),
            ServerId.New(),
            TestData.CpuRule(severity: Severity.Warning),
            TestData.Now);

        incident.Escalate(Severity.Critical, TestData.Now.AddMinutes(5));

        Assert.Equal(Severity.Critical, incident.Severity);
    }

    /// <summary>
    /// La gravité ne redescend jamais : abaisser celle d'un incident déjà notifié masquerait
    /// l'épisode réel dans l'historique.
    /// </summary>
    [Fact]
    public void EscalationNeverLowersSeverity()
    {
        var incident = Incident.Open(
            IncidentId.New(),
            ServerId.New(),
            TestData.CpuRule(severity: Severity.Critical),
            TestData.Now);

        incident.Escalate(Severity.Information, TestData.Now.AddMinutes(5));

        Assert.Equal(Severity.Critical, incident.Severity);
    }

    [Fact]
    public void DurationRunsWhileOpenAndFreezesOnResolution()
    {
        var incident = OpenIncident();

        Assert.Equal(TimeSpan.FromMinutes(30), incident.DurationAt(TestData.Now.AddMinutes(30)));

        incident.Resolve(TestData.Now.AddMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(45), incident.DurationAt(TestData.Now.AddHours(5)));
    }

    [Fact]
    public void EventsGoingBackwardsInTimeAreRejected()
    {
        var incident = OpenIncident();
        incident.RecordObservation(93d, TestData.Now.AddMinutes(5));

        Assert.Throws<DomainValidationException>(() =>
            incident.Acknowledge("ops-team", TestData.Now.AddMinutes(4)));
    }

    [Fact]
    public void TimestampsAreNormalisedToUtc()
    {
        var incident = Incident.Open(
            IncidentId.New(),
            ServerId.New(),
            TestData.CpuRule(),
            new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(TimeSpan.Zero, incident.StartedAt.Offset);
        Assert.Equal(10, incident.StartedAt.Hour);
    }

    [Fact]
    public void StateBasedRuleProducesAnIncidentWithoutThreshold()
    {
        var incident = Incident.Open(
            IncidentId.New(),
            ServerId.New(),
            TestData.UnavailableRule(),
            TestData.Now);

        Assert.Null(incident.ThresholdValue);
        Assert.Equal(MonitoredMetric.ServerUnavailable, incident.Metric);
    }
}
