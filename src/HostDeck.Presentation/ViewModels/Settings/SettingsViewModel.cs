using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HostDeck.Application.Dtos.Settings;
using HostDeck.Application.Settings;

namespace HostDeck.Presentation.ViewModels.Settings;

/// <summary>Paramètres globaux branchés sur les use cases Application (pas d'infra directe).</summary>
public partial class SettingsViewModel : PageViewModelBase
{
    private readonly GetSettingsUseCase _getSettings;
    private readonly UpdateSettingsUseCase _updateSettings;

    [ObservableProperty]
    private string _instanceName = "HostDeck";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private int _defaultCollectionIntervalSeconds = 60;

    [ObservableProperty]
    private int _collectionTimeoutSeconds = 10;

    [ObservableProperty]
    private int _collectionRetryCount = 3;

    [ObservableProperty]
    private int _maxConcurrentCollections = 8;

    [ObservableProperty]
    private int _metricRetentionDays = 90;

    [ObservableProperty]
    private int _eventRetentionDays = 180;

    [ObservableProperty]
    private bool _desktopNotificationsEnabled = true;

    [ObservableProperty]
    private bool _autoRefreshEnabled = true;

    [ObservableProperty]
    private int _autoRefreshIntervalSeconds = 30;

    [ObservableProperty]
    private string? _storageDirectory;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsViewModel(GetSettingsUseCase getSettings, UpdateSettingsUseCase updateSettings)
    {
        _getSettings = getSettings;
        _updateSettings = updateSettings;
    }

    public override string Title => "Paramètres";

    public override string Breadcrumb => "Paramètres › Général";

    public override string StatusSummary => "Paramètres locaux";

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var dto = await _getSettings.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Apply(dto);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var dto = new SettingsDto
        {
            InstanceName = InstanceName,
            Description = Description,
            DefaultCollectionIntervalSeconds = DefaultCollectionIntervalSeconds,
            CollectionTimeoutSeconds = CollectionTimeoutSeconds,
            CollectionRetryCount = CollectionRetryCount,
            MaxConcurrentCollections = MaxConcurrentCollections,
            MetricRetentionDays = MetricRetentionDays,
            EventRetentionDays = EventRetentionDays,
            DesktopNotificationsEnabled = DesktopNotificationsEnabled,
            AutoRefreshEnabled = AutoRefreshEnabled,
            AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds,
            StorageDirectory = StorageDirectory,
        };

        await _updateSettings.ExecuteAsync(dto, cancellationToken).ConfigureAwait(true);
        StatusMessage = "Paramètres enregistrés.";
    }

    private void Apply(SettingsDto dto)
    {
        InstanceName = dto.InstanceName;
        Description = dto.Description;
        DefaultCollectionIntervalSeconds = dto.DefaultCollectionIntervalSeconds;
        CollectionTimeoutSeconds = dto.CollectionTimeoutSeconds;
        CollectionRetryCount = dto.CollectionRetryCount;
        MaxConcurrentCollections = dto.MaxConcurrentCollections;
        MetricRetentionDays = dto.MetricRetentionDays;
        EventRetentionDays = dto.EventRetentionDays;
        DesktopNotificationsEnabled = dto.DesktopNotificationsEnabled;
        AutoRefreshEnabled = dto.AutoRefreshEnabled;
        AutoRefreshIntervalSeconds = dto.AutoRefreshIntervalSeconds;
        StorageDirectory = dto.StorageDirectory;
    }
}
