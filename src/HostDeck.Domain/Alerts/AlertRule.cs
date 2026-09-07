using System;
using HostDeck.Domain.Incidents;
using HostDeck.Domain.Shared;

namespace HostDeck.Domain.Alerts;

/// <summary>Identifiant d'une règle d'alerte.</summary>
public readonly record struct AlertRuleId
{
    public AlertRuleId(Guid value)
    {
        Value = Guard.NotEmpty(value);
    }

    public Guid Value { get; }

    public static AlertRuleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Règle d'alerte : une condition, une durée de confirmation, une gravité et un périmètre.
///
/// Deux mécanismes distincts protègent contre le bruit, et ils ne servent pas à la même
/// chose (§20) :
/// <list type="bullet">
///   <item><see cref="Duration"/> évite d'ouvrir un incident sur un pic instantané : la
///   condition doit tenir pendant toute la durée avant de compter.</item>
///   <item><see cref="Cooldown"/> évite de re-notifier en boucle un incident déjà connu.</item>
/// </list>
/// </summary>
public sealed class AlertRule
{
    public const int MaxNameLength = 128;
    public const int MaxDescriptionLength = 512;

    public static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaximumCooldown = TimeSpan.FromDays(7);

    public AlertRule(
        AlertRuleId id,
        string name,
        MonitoredMetric metric,
        Severity severity,
        AlertScope scope,
        Threshold? threshold = null,
        TimeSpan? duration = null,
        TimeSpan? cooldown = null,
        string? description = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(scope);

        EnsureThresholdMatchesMetric(metric, threshold);

        Id = id;
        Name = Guard.RequiredText(name, MaxNameLength);
        Metric = metric;
        Severity = severity;
        Scope = scope;
        Threshold = threshold;
        Duration = ValidateDuration(duration ?? TimeSpan.Zero);
        Cooldown = ValidateCooldown(cooldown ?? TimeSpan.Zero);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsEnabled = enabled;
    }

    public AlertRuleId Id { get; }

    public string Name { get; private set; }

    public MonitoredMetric Metric { get; }

    public Severity Severity { get; private set; }

    public AlertScope Scope { get; private set; }

    /// <summary>Seuil numérique, absent pour les métriques d'état binaire.</summary>
    public Threshold? Threshold { get; private set; }

    /// <summary>Durée pendant laquelle la condition doit tenir avant de déclencher.</summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>Délai minimal entre deux notifications pour un même incident.</summary>
    public TimeSpan Cooldown { get; private set; }

    public string? Description { get; private set; }

    public bool IsEnabled { get; private set; }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void Rename(string name) => Name = Guard.RequiredText(name, MaxNameLength);

    public void ChangeSeverity(Severity severity) => Severity = severity;

    public void ChangeScope(AlertScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
    }

    public void ChangeThreshold(Threshold? threshold)
    {
        EnsureThresholdMatchesMetric(Metric, threshold);
        Threshold = threshold;
    }

    public void ChangeTiming(TimeSpan duration, TimeSpan cooldown)
    {
        Duration = ValidateDuration(duration);
        Cooldown = ValidateCooldown(cooldown);
    }

    public void ChangeDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// Évalue si la condition de la règle est remplie par une valeur observée.
    /// Ne dit rien de la durée ni du cooldown : c'est au moteur d'incidents de les appliquer.
    /// </summary>
    public bool IsConditionMet(double observedValue) =>
        Threshold?.IsBreachedBy(observedValue) ?? false;

    /// <summary>
    /// Détermine si une notification peut partir, compte tenu de la dernière déjà envoyée.
    /// </summary>
    public bool AllowsNotificationAt(DateTimeOffset now, DateTimeOffset? lastNotifiedAt)
    {
        if (lastNotifiedAt is not { } previous)
        {
            return true;
        }

        return now - previous >= Cooldown;
    }

    private static void EnsureThresholdMatchesMetric(MonitoredMetric metric, Threshold? threshold)
    {
        if (metric.IsStateBased())
        {
            if (threshold is not null)
            {
                throw new DomainValidationException(
                    nameof(threshold),
                    $"la métrique {metric} est un état binaire et n'admet pas de seuil");
            }

            return;
        }

        if (threshold is null)
        {
            throw new DomainValidationException(
                nameof(threshold),
                $"la métrique {metric} exige un seuil");
        }

        if (threshold.Metric != metric)
        {
            throw new DomainValidationException(
                nameof(threshold),
                $"le seuil porte sur {threshold.Metric} alors que la règle porte sur {metric}");
        }
    }

    private static TimeSpan ValidateDuration(TimeSpan duration) =>
        Guard.InRange(duration, TimeSpan.Zero, MaximumDuration, nameof(duration));

    private static TimeSpan ValidateCooldown(TimeSpan cooldown) =>
        Guard.InRange(cooldown, TimeSpan.Zero, MaximumCooldown, nameof(cooldown));
}
