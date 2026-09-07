using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Settings;
using HostDeck.Application.Settings;
using HostDeck.Application.Tests.Fakes;
using HostDeck.Domain.Incidents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
// FluentValidation expose son propre type Severity : l'alias lève l'ambiguïté
// sans masquer laquelle des deux est utilisée.
using Severity = HostDeck.Domain.Incidents.Severity;

namespace HostDeck.Application.Tests.Settings;

public sealed class SettingsUseCaseTests
{
    private readonly FakeSettingsRepository _settings = new();
    private readonly UpdateSettingsUseCase _update;
    private readonly GetSettingsUseCase _get;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public SettingsUseCaseTests()
    {
        _get = new GetSettingsUseCase(_settings);
        _update = new UpdateSettingsUseCase(
            _settings,
            new SettingsDtoValidator(),
            NullLogger<UpdateSettingsUseCase>.Instance);
    }

    private static SettingsDto ValidSettings() => new()
    {
        InstanceName = "HostDeck Production",
        DefaultCollectionIntervalSeconds = 60,
        CollectionTimeoutSeconds = 10,
        CollectionRetryCount = 3,
        MaxConcurrentCollections = 8,
        MetricRetentionDays = 90,
        EventRetentionDays = 180,
        AutoRefreshIntervalSeconds = 30,
    };

    [Fact]
    public async Task DefaultsMatchTheSettingsScreen()
    {
        var settings = await _get.ExecuteAsync(Ct);

        Assert.Equal(60, settings.DefaultCollectionIntervalSeconds);
        Assert.Equal(10, settings.CollectionTimeoutSeconds);
        Assert.Equal(3, settings.CollectionRetryCount);
        Assert.Equal(90, settings.MetricRetentionDays);
        Assert.Equal(180, settings.EventRetentionDays);
    }

    [Fact]
    public async Task SavesAndReadsBack()
    {
        var request = ValidSettings() with
        {
            InstanceName = "HostDeck Staging",
            MetricRetentionDays = 30,
            MinimumNotificationSeverity = Severity.High,
        };

        await _update.ExecuteAsync(request, Ct);
        var reloaded = await _get.ExecuteAsync(Ct);

        Assert.Equal("HostDeck Staging", reloaded.InstanceName);
        Assert.Equal(30, reloaded.MetricRetentionDays);
        Assert.Equal(Severity.High, reloaded.MinimumNotificationSeverity);
    }

    /// <summary>
    /// La borne basse de l'intervalle empêche HostDeck de devenir une source de charge sur
    /// la production supervisée (§51, T8).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7200)]
    public async Task RejectsACollectionIntervalOutOfRange(int seconds)
    {
        var request = ValidSettings() with { DefaultCollectionIntervalSeconds = seconds };

        await Assert.ThrowsAsync<ValidationException>(() => _update.ExecuteAsync(request, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task RejectsAConcurrencyOutOfRange(int concurrency)
    {
        var request = ValidSettings() with { MaxConcurrentCollections = concurrency };

        await Assert.ThrowsAsync<ValidationException>(() => _update.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsAnEmptyInstanceName()
    {
        var request = ValidSettings() with { InstanceName = "  " };

        await Assert.ThrowsAsync<ValidationException>(() => _update.ExecuteAsync(request, Ct));
    }

    [Fact]
    public async Task RejectsARetentionBelowOneDay()
    {
        var request = ValidSettings() with { MetricRetentionDays = 0 };

        await Assert.ThrowsAsync<ValidationException>(() => _update.ExecuteAsync(request, Ct));
    }

    /// <summary>
    /// La conversion vers la politique de rétention du domaine doit rester cohérente avec ce
    /// que l'écran Paramètres enregistre.
    /// </summary>
    [Fact]
    public async Task ConvertsToTheDomainRetentionPolicy()
    {
        await _update.ExecuteAsync(
            ValidSettings() with { MetricRetentionDays = 45, EventRetentionDays = 120 },
            Ct);

        var policy = _settings.Current.ToRetentionPolicy();

        Assert.Equal(TimeSpan.FromDays(45), policy.MetricRetention);
        Assert.Equal(TimeSpan.FromDays(120), policy.EventRetention);
    }
}
