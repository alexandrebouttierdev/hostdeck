using System;
using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Servers.Validation;

/// <summary>
/// Règles communes aux DTO qui décrivent un point de connexion SSH.
///
/// La validation ici porte sur la <em>forme</em> de la saisie, pour produire un message
/// utilisable dans le formulaire. Elle ne remplace pas les invariants du domaine : ceux-ci
/// restent tenus par les constructeurs, de sorte qu'un chemin qui contournerait la validation
/// ne puisse pas construire un modèle invalide (§7, ARCHITECTURE.md).
/// </summary>
internal static class EndpointRules
{
    public static IRuleBuilderOptions<T, string> ValidHostAddress<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("L'adresse est obligatoire.")
            .Must(value => HostAddress.TryParse(value, out _))
            .WithMessage("L'adresse doit être un nom DNS ou une adresse IP valide.");

    public static IRuleBuilderOptions<T, string> ValidSshUsername<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Le nom d'utilisateur est obligatoire.")
            .MaximumLength(SshUsername.MaxLength)
            .WithMessage($"Le nom d'utilisateur ne peut pas dépasser {SshUsername.MaxLength} caractères.")
            .Must(BeAPosixUsername)
            .WithMessage("Le nom d'utilisateur contient un caractère non autorisé.");

    public static IRuleBuilderOptions<T, int> ValidPort<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(Port.Minimum, Port.Maximum)
            .WithMessage($"Le port doit être compris entre {Port.Minimum} et {Port.Maximum}.");

    public static IRuleBuilderOptions<T, int> ValidMonitoringInterval<T>(
        this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(
                (int)MonitoringInterval.MinimumValue.TotalSeconds,
                (int)MonitoringInterval.MaximumValue.TotalSeconds)
            .WithMessage(
                $"L'intervalle de collecte doit être compris entre "
                + $"{MonitoringInterval.MinimumValue.TotalSeconds:0} et "
                + $"{MonitoringInterval.MaximumValue.TotalSeconds:0} secondes.");

    private static bool BeAPosixUsername(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '-')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.' or '$'))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class JumpHostDtoValidator : AbstractValidator<JumpHostDto>
{
    public JumpHostDtoValidator()
    {
        RuleFor(x => x.Address).ValidHostAddress();
        RuleFor(x => x.Port).ValidPort();
        RuleFor(x => x.Username).ValidSshUsername();
        RuleFor(x => x.CredentialKey)
            .NotEmpty().WithMessage("Le bastion doit référencer un identifiant.");
    }
}

public sealed class CreateServerDtoValidator : AbstractValidator<CreateServerDto>
{
    public CreateServerDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom est obligatoire.")
            .MaximumLength(ServerName.MaxLength)
            .WithMessage($"Le nom ne peut pas dépasser {ServerName.MaxLength} caractères.");

        RuleFor(x => x.Address).ValidHostAddress();
        RuleFor(x => x.Port).ValidPort();
        RuleFor(x => x.Username).ValidSshUsername();
        RuleFor(x => x.MonitoringIntervalSeconds).ValidMonitoringInterval();

        RuleFor(x => x.Secret)
            .NotEmpty().WithMessage("Un identifiant est obligatoire pour se connecter au serveur.");

        RuleFor(x => x.Group)
            .MaximumLength(ServerGroup.MaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Group))
            .WithMessage($"Le nom du groupe ne peut pas dépasser {ServerGroup.MaxLength} caractères.");

        RuleForEach(x => x.Tags)
            .NotEmpty().WithMessage("Un tag ne peut pas être vide.")
            .MaximumLength(ServerTag.MaxLength)
            .WithMessage($"Un tag ne peut pas dépasser {ServerTag.MaxLength} caractères.");

        RuleFor(x => x.JumpHost!)
            .SetValidator(new JumpHostDtoValidator())
            .When(x => x.JumpHost is not null);

        // Cas de cycle du §41 : un bastion qui pointe sur sa propre cible ne fournit
        // aucun chemin. Vérifié ici pour produire un message de formulaire, et de nouveau
        // par le domaine, qui reste la garantie effective.
        RuleFor(x => x)
            .Must(NotUseTargetAsItsOwnJumpHost)
            .WithName(nameof(CreateServerDto.JumpHost))
            .WithMessage("Le bastion ne peut pas être le serveur cible lui-même.");
    }

    private static bool NotUseTargetAsItsOwnJumpHost(CreateServerDto dto) =>
        dto.JumpHost is null
        || dto.JumpHost.Port != dto.Port
        || !string.Equals(dto.JumpHost.Address, dto.Address, StringComparison.OrdinalIgnoreCase);
}

public sealed class UpdateServerDtoValidator : AbstractValidator<UpdateServerDto>
{
    public UpdateServerDtoValidator()
    {
        RuleFor(x => x.ServerId)
            .NotEqual(Guid.Empty).WithMessage("L'identifiant du serveur est obligatoire.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom est obligatoire.")
            .MaximumLength(ServerName.MaxLength)
            .WithMessage($"Le nom ne peut pas dépasser {ServerName.MaxLength} caractères.");

        RuleFor(x => x.Address).ValidHostAddress();
        RuleFor(x => x.Port).ValidPort();
        RuleFor(x => x.Username).ValidSshUsername();
        RuleFor(x => x.MonitoringIntervalSeconds).ValidMonitoringInterval();

        RuleFor(x => x.Group)
            .MaximumLength(ServerGroup.MaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Group))
            .WithMessage($"Le nom du groupe ne peut pas dépasser {ServerGroup.MaxLength} caractères.");

        RuleForEach(x => x.Tags)
            .NotEmpty().WithMessage("Un tag ne peut pas être vide.")
            .MaximumLength(ServerTag.MaxLength)
            .WithMessage($"Un tag ne peut pas dépasser {ServerTag.MaxLength} caractères.");

        RuleFor(x => x.JumpHost!)
            .SetValidator(new JumpHostDtoValidator())
            .When(x => x.JumpHost is not null);

        RuleFor(x => x)
            .Must(NotUseTargetAsItsOwnJumpHost)
            .WithName(nameof(UpdateServerDto.JumpHost))
            .WithMessage("Le bastion ne peut pas être le serveur cible lui-même.");
    }

    private static bool NotUseTargetAsItsOwnJumpHost(UpdateServerDto dto) =>
        dto.JumpHost is null
        || dto.JumpHost.Port != dto.Port
        || !string.Equals(dto.JumpHost.Address, dto.Address, StringComparison.OrdinalIgnoreCase);
}

public sealed class TestConnectionRequestDtoValidator : AbstractValidator<TestConnectionRequestDto>
{
    public TestConnectionRequestDtoValidator()
    {
        RuleFor(x => x.Address).ValidHostAddress();
        RuleFor(x => x.Port).ValidPort();
        RuleFor(x => x.Username).ValidSshUsername();

        RuleFor(x => x)
            .Must(HaveCredentialOrSecret)
            .WithName(nameof(TestConnectionRequestDto.CredentialKey))
            .WithMessage("Un identifiant ou un secret est obligatoire pour tester la connexion.");

        RuleFor(x => x.CredentialKey)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Secret))
            .WithMessage("Un identifiant est obligatoire pour tester la connexion.");

        RuleFor(x => x.Secret)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.CredentialKey))
            .WithMessage("Un secret est obligatoire pour tester la connexion.");

        RuleFor(x => x.Timeout)
            .InclusiveBetween(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2))
            .WithMessage("Le délai d'attente doit être compris entre 1 seconde et 2 minutes.");

        RuleFor(x => x.JumpHost!)
            .SetValidator(new JumpHostDtoValidator())
            .When(x => x.JumpHost is not null);
    }

    private static bool HaveCredentialOrSecret(TestConnectionRequestDto dto) =>
        !string.IsNullOrEmpty(dto.CredentialKey) || !string.IsNullOrEmpty(dto.Secret);
}
