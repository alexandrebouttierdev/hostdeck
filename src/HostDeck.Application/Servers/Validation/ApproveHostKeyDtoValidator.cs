using FluentValidation;
using HostDeck.Application.Dtos.Servers;
using HostDeck.Domain.Servers;

namespace HostDeck.Application.Servers.Validation;

public sealed class ApproveHostKeyDtoValidator : AbstractValidator<ApproveHostKeyDto>
{
    public ApproveHostKeyDtoValidator()
    {
        RuleFor(x => x.Host).ValidHostAddress();
        RuleFor(x => x.Port).ValidPort();
        RuleFor(x => x.Fingerprint)
            .NotEmpty().WithMessage("L'empreinte de clé d'hôte est obligatoire.")
            .MaximumLength(128).WithMessage("L'empreinte de clé d'hôte est trop longue.");
    }
}
