namespace Application.Features.StatusFeatures.Validators;

using Application.Features.StatusFeatures.Commands;

public class UpdateStatusCommandValidator : AbstractValidator<UpdateStatusCommand>
{
    public UpdateStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
