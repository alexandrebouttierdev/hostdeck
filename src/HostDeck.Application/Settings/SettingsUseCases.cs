using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using HostDeck.Application.Dtos.Settings;
using HostDeck.Application.Logging;
using HostDeck.Application.Ports;
using HostDeck.Domain.Monitoring;
using HostDeck.Domain.Servers;
using Microsoft.Extensions.Logging;

namespace HostDeck.Application.Settings;

/// <summary>Lit les paramètres globaux.</summary>
public sealed class GetSettingsUseCase
{
    private readonly ISettingsRepository _settings;

    public GetSettingsUseCase(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<SettingsDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        return SettingsMapper.ToDto(snapshot);
    }
}

/// <summary>Enregistre les paramètres globaux.</summary>
public sealed class UpdateSettingsUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IValidator<SettingsDto> _validator;
    private readonly ILogger<UpdateSettingsUseCase> _logger;

    public UpdateSettingsUseCase(
        ISettingsRepository settings,
        IValidator<SettingsDto> validator,
        ILogger<UpdateSettingsUseCase> logger)
    {
        _settings = settings;
        _validator = validator;
        _logger = logger;
    }

    public async Task ExecuteAsync(SettingsDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        await _settings
            .SaveAsync(SettingsMapper.ToSnapshot(request), cancellationToken)
            .ConfigureAwait(false);

        ApplicationLog.SettingsUpdated(_logger);
    }
}

/// <summary>Conversions entre les paramètres persistés et leur DTO.</summary>
public static class SettingsMapper
{
    public static SettingsDto ToDto(SettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SettingsDto
        {
            InstanceName = snapshot.InstanceName,
            Description = snapshot.Description,
            DefaultCollectionIntervalSeconds = snapshot.DefaultCollectionIntervalSeconds,
            CollectionTimeoutSeconds = snapshot.CollectionTimeoutSeconds,
            CollectionRetryCount = snapshot.CollectionRetryCount,
            MaxConcurrentCollections = snapshot.MaxConcurrentCollections,
            MetricRetentionDays = snapshot.MetricRetentionDays,
            EventRetentionDays = snapshot.EventRetentionDays,
            DesktopNotificationsEnabled = snapshot.DesktopNotificationsEnabled,
            MinimumNotificationSeverity = snapshot.MinimumNotificationSeverity,
            AutoRefreshEnabled = snapshot.AutoRefreshEnabled,
            AutoRefreshIntervalSeconds = snapshot.AutoRefreshIntervalSeconds,
            StorageDirectory = snapshot.StorageDirectory,
        };
    }

    public static SettingsSnapshot ToSnapshot(SettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SettingsSnapshot
        {
            InstanceName = dto.InstanceName,
            Description = dto.Description,
            DefaultCollectionIntervalSeconds = dto.DefaultCollectionIntervalSeconds,
            CollectionTimeoutSeconds = dto.CollectionTimeoutSeconds,
            CollectionRetryCount = dto.CollectionRetryCount,
            MaxConcurrentCollections = dto.MaxConcurrentCollections,
            MetricRetentionDays = dto.MetricRetentionDays,
            EventRetentionDays = dto.EventRetentionDays,
            DesktopNotificationsEnabled = dto.DesktopNotificationsEnabled,
            MinimumNotificationSeverity = dto.MinimumNotificationSeverity,
            AutoRefreshEnabled = dto.AutoRefreshEnabled,
            AutoRefreshIntervalSeconds = dto.AutoRefreshIntervalSeconds,
            StorageDirectory = dto.StorageDirectory,
        };
    }
}

/// <summary>
/// Validation des paramètres globaux.
///
/// Les bornes sont celles du domaine, pas des préférences : un intervalle trop court fait de
/// HostDeck une source de charge sur la production, une concurrence trop élevée sature le
/// poste et les hôtes supervisés (§51, T8).
/// </summary>
public sealed class SettingsDtoValidator : AbstractValidator<SettingsDto>
{
    public SettingsDtoValidator()
    {
        RuleFor(x => x.InstanceName)
            .NotEmpty().WithMessage("Le nom de l'instance est obligatoire.")
            .MaximumLength(64).WithMessage("Le nom de l'instance ne peut pas dépasser 64 caractères.");

        RuleFor(x => x.Description)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("La description ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.DefaultCollectionIntervalSeconds)
            .InclusiveBetween(
                (int)MonitoringInterval.MinimumValue.TotalSeconds,
                (int)MonitoringInterval.MaximumValue.TotalSeconds)
            .WithMessage(
                $"L'intervalle de collecte doit être compris entre "
                + $"{MonitoringInterval.MinimumValue.TotalSeconds:0} et "
                + $"{MonitoringInterval.MaximumValue.TotalSeconds:0} secondes.");

        RuleFor(x => x.CollectionTimeoutSeconds)
            .InclusiveBetween(1, 300)
            .WithMessage("Le délai d'attente doit être compris entre 1 et 300 secondes.");

        RuleFor(x => x.CollectionRetryCount)
            .InclusiveBetween(0, 10)
            .WithMessage("Le nombre de tentatives doit être compris entre 0 et 10.");

        RuleFor(x => x.MaxConcurrentCollections)
            .InclusiveBetween(1, 64)
            .WithMessage("La collecte simultanée doit porter sur 1 à 64 hôtes.");

        RuleFor(x => x.MetricRetentionDays)
            .InclusiveBetween(
                (int)RetentionPolicy.MinimumRetention.TotalDays,
                (int)RetentionPolicy.MaximumRetention.TotalDays)
            .WithMessage("La rétention des métriques doit être comprise entre 1 et 3650 jours.");

        RuleFor(x => x.EventRetentionDays)
            .InclusiveBetween(
                (int)RetentionPolicy.MinimumRetention.TotalDays,
                (int)RetentionPolicy.MaximumRetention.TotalDays)
            .WithMessage("La rétention des événements doit être comprise entre 1 et 3650 jours.");

        RuleFor(x => x.AutoRefreshIntervalSeconds)
            .InclusiveBetween(1, 3600)
            .WithMessage("L'intervalle d'actualisation doit être compris entre 1 et 3600 secondes.");
    }
}
