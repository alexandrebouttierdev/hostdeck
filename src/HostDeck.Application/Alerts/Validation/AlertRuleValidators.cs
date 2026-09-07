using System;
using FluentValidation;
using HostDeck.Application.Dtos.Alerts;
using HostDeck.Domain.Alerts;

namespace HostDeck.Application.Alerts.Validation;

/// <summary>
/// Règles de saisie communes aux DTO de règle d'alerte.
///
/// Elles reproduisent volontairement, au niveau du formulaire, des contraintes que le domaine
/// tient aussi : ici pour produire un message actionnable, là-bas pour garantir qu'aucun
/// chemin ne construise une règle invalide.
/// </summary>
internal static class AlertRuleRules
{
    public static IRuleBuilderOptions<T, TimeSpan> ValidDuration<T>(
        this IRuleBuilder<T, TimeSpan> rule) =>
        rule.InclusiveBetween(TimeSpan.Zero, AlertRule.MaximumDuration)
            .WithMessage(
                $"La durée doit être comprise entre 0 et {AlertRule.MaximumDuration.TotalHours:0} heures.");

    public static IRuleBuilderOptions<T, TimeSpan> ValidCooldown<T>(
        this IRuleBuilder<T, TimeSpan> rule) =>
        rule.InclusiveBetween(TimeSpan.Zero, AlertRule.MaximumCooldown)
            .WithMessage(
                $"Le temps de refroidissement doit être compris entre 0 et {AlertRule.MaximumCooldown.TotalDays:0} jours.");
}

public sealed class CreateAlertRuleDtoValidator : AbstractValidator<CreateAlertRuleDto>
{
    public CreateAlertRuleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom de la règle est obligatoire.")
            .MaximumLength(AlertRule.MaxNameLength)
            .WithMessage($"Le nom ne peut pas dépasser {AlertRule.MaxNameLength} caractères.");

        RuleFor(x => x.Description)
            .MaximumLength(AlertRule.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage($"La description ne peut pas dépasser {AlertRule.MaxDescriptionLength} caractères.");

        RuleFor(x => x.Duration).ValidDuration();
        RuleFor(x => x.Cooldown).ValidCooldown();

        // Une règle numérique sans seuil ne se déclencherait jamais.
        RuleFor(x => x.ThresholdValue)
            .NotNull()
            .When(x => !x.Metric.IsStateBased())
            .WithMessage("Cette métrique exige un seuil.");

        RuleFor(x => x.Comparison)
            .NotNull()
            .When(x => !x.Metric.IsStateBased())
            .WithMessage("Cette métrique exige un opérateur de comparaison.");

        // Un seuil sur une métrique d'état binaire n'a aucun sens : la condition est le fait.
        RuleFor(x => x.ThresholdValue)
            .Null()
            .When(x => x.Metric.IsStateBased())
            .WithMessage("Cette métrique est un état et n'admet pas de seuil.");

        // Un seuil CPU à 150 % ne serait jamais franchi : la règle serait inerte.
        RuleFor(x => x.ThresholdValue!.Value)
            .InclusiveBetween(0d, 100d)
            .When(x => x.Metric.IsPercentage() && x.ThresholdValue is not null)
            .WithMessage("Un seuil en pourcentage doit être compris entre 0 et 100.");

        RuleFor(x => x.ThresholdValue!.Value)
            .GreaterThanOrEqualTo(0d)
            .When(x => !x.Metric.IsPercentage() && x.ThresholdValue is not null)
            .WithMessage("Le seuil ne peut pas être négatif.");

        RuleFor(x => x.ScopeGroup)
            .NotEmpty()
            .When(x => x.ScopeKind == AlertScopeKind.Group)
            .WithMessage("Un périmètre de groupe exige un nom de groupe.");

        RuleFor(x => x.ScopeServerId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.ScopeKind == AlertScopeKind.Server)
            .WithMessage("Un périmètre de serveur exige un serveur.");
    }
}

public sealed class UpdateAlertRuleDtoValidator : AbstractValidator<UpdateAlertRuleDto>
{
    public UpdateAlertRuleDtoValidator()
    {
        RuleFor(x => x.RuleId)
            .NotEqual(Guid.Empty).WithMessage("L'identifiant de la règle est obligatoire.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom de la règle est obligatoire.")
            .MaximumLength(AlertRule.MaxNameLength)
            .WithMessage($"Le nom ne peut pas dépasser {AlertRule.MaxNameLength} caractères.");

        RuleFor(x => x.Description)
            .MaximumLength(AlertRule.MaxDescriptionLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage($"La description ne peut pas dépasser {AlertRule.MaxDescriptionLength} caractères.");

        RuleFor(x => x.Duration).ValidDuration();
        RuleFor(x => x.Cooldown).ValidCooldown();

        RuleFor(x => x.ScopeGroup)
            .NotEmpty()
            .When(x => x.ScopeKind == AlertScopeKind.Group)
            .WithMessage("Un périmètre de groupe exige un nom de groupe.");

        RuleFor(x => x.ScopeServerId)
            .NotNull().NotEqual(Guid.Empty)
            .When(x => x.ScopeKind == AlertScopeKind.Server)
            .WithMessage("Un périmètre de serveur exige un serveur.");
    }
}

public sealed class AcknowledgeIncidentDtoValidator
    : AbstractValidator<Dtos.Incidents.AcknowledgeIncidentDto>
{
    public AcknowledgeIncidentDtoValidator()
    {
        RuleFor(x => x.IncidentId)
            .NotEqual(Guid.Empty).WithMessage("L'identifiant de l'incident est obligatoire.");

        RuleFor(x => x.AcknowledgedBy)
            .NotEmpty().WithMessage("L'acquittement doit identifier son auteur.")
            .MaximumLength(Domain.Incidents.Incident.MaxAcknowledgedByLength)
            .WithMessage(
                $"Le nom ne peut pas dépasser {Domain.Incidents.Incident.MaxAcknowledgedByLength} caractères.");
    }
}
