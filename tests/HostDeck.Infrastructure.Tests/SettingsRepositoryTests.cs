using System.Threading.Tasks;
using HostDeck.Domain.Incidents;
using Xunit;

namespace HostDeck.Infrastructure.Tests;

public sealed class SettingsRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Get_ReturnsDefaultsWhenEmpty()
    {
        var settings = await Settings.GetAsync(Ct);

        Assert.Equal("HostDeck", settings.InstanceName);
        Assert.Equal(60, settings.DefaultCollectionIntervalSeconds);
        Assert.Equal(90, settings.MetricRetentionDays);
        Assert.True(settings.DesktopNotificationsEnabled);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsValues()
    {
        var saved = SettingsSnapshotFactory.Custom();

        await Settings.SaveAsync(saved, Ct);

        var reloaded = await Settings.GetAsync(Ct);
        Assert.Equal("Ma flotte", reloaded.InstanceName);
        Assert.Equal("Serveurs de production", reloaded.Description);
        Assert.Equal(30, reloaded.DefaultCollectionIntervalSeconds);
        Assert.Equal(45, reloaded.MetricRetentionDays);
        Assert.Equal(Severity.Critical, reloaded.MinimumNotificationSeverity);
        Assert.Equal("/var/lib/hostdeck", reloaded.StorageDirectory);
    }

    [Fact]
    public async Task SaveTwice_KeepsSingleRow()
    {
        await Settings.SaveAsync(SettingsSnapshotFactory.Custom(), Ct);

        var updated = SettingsSnapshotFactory.Custom() with
        {
            InstanceName = "Renommée",
        };
        await Settings.SaveAsync(updated, Ct);

        var reloaded = await Settings.GetAsync(Ct);
        Assert.Equal("Renommée", reloaded.InstanceName);
        Assert.Equal("Serveurs de production", reloaded.Description);
    }
}

/// <summary>Fabrique d'un instantané de paramètres non trivial.</summary>
internal static class SettingsSnapshotFactory
{
    public static HostDeck.Application.Ports.SettingsSnapshot Custom() =>
        new()
        {
            InstanceName = "Ma flotte",
            Description = "Serveurs de production",
            DefaultCollectionIntervalSeconds = 30,
            CollectionTimeoutSeconds = 15,
            CollectionRetryCount = 2,
            MaxConcurrentCollections = 4,
            MetricRetentionDays = 45,
            EventRetentionDays = 90,
            DesktopNotificationsEnabled = false,
            MinimumNotificationSeverity = Severity.Critical,
            AutoRefreshEnabled = false,
            AutoRefreshIntervalSeconds = 60,
            StorageDirectory = "/var/lib/hostdeck",
        };
}
