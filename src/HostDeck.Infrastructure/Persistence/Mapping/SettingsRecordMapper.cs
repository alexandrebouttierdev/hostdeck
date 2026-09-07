using System;
using HostDeck.Application.Ports;
using HostDeck.Domain.Incidents;
using HostDeck.Infrastructure.Persistence.Records;

namespace HostDeck.Infrastructure.Persistence.Mapping;

/// <summary>Conversions entre <see cref="SettingsSnapshot"/> et <see cref="SettingsRecord"/>.</summary>
internal static class SettingsRecordMapper
{
    public static SettingsSnapshot ToSnapshot(SettingsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new SettingsSnapshot
        {
            InstanceName = record.InstanceName,
            Description = record.Description,
            DefaultCollectionIntervalSeconds = record.DefaultCollectionIntervalSeconds,
            CollectionTimeoutSeconds = record.CollectionTimeoutSeconds,
            CollectionRetryCount = record.CollectionRetryCount,
            MaxConcurrentCollections = record.MaxConcurrentCollections,
            MetricRetentionDays = record.MetricRetentionDays,
            EventRetentionDays = record.EventRetentionDays,
            DesktopNotificationsEnabled = record.DesktopNotificationsEnabled,
            MinimumNotificationSeverity = (Severity)record.MinimumNotificationSeverity,
            AutoRefreshEnabled = record.AutoRefreshEnabled,
            AutoRefreshIntervalSeconds = record.AutoRefreshIntervalSeconds,
            StorageDirectory = record.StorageDirectory,
        };
    }

    public static void ApplyTo(SettingsRecord record, SettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(snapshot);

        record.InstanceName = snapshot.InstanceName;
        record.Description = snapshot.Description;
        record.DefaultCollectionIntervalSeconds = snapshot.DefaultCollectionIntervalSeconds;
        record.CollectionTimeoutSeconds = snapshot.CollectionTimeoutSeconds;
        record.CollectionRetryCount = snapshot.CollectionRetryCount;
        record.MaxConcurrentCollections = snapshot.MaxConcurrentCollections;
        record.MetricRetentionDays = snapshot.MetricRetentionDays;
        record.EventRetentionDays = snapshot.EventRetentionDays;
        record.DesktopNotificationsEnabled = snapshot.DesktopNotificationsEnabled;
        record.MinimumNotificationSeverity = (int)snapshot.MinimumNotificationSeverity;
        record.AutoRefreshEnabled = snapshot.AutoRefreshEnabled;
        record.AutoRefreshIntervalSeconds = snapshot.AutoRefreshIntervalSeconds;
        record.StorageDirectory = snapshot.StorageDirectory;
    }
}
