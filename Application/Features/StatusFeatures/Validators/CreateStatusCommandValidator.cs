namespace Application.Features.StatusFeatures.Validators;

using Application.Features.StatusFeatures.Commands;

public class CreateStatusCommandValidator : AbstractValidator<CreateStatusCommand>
{
    public CreateStatusCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
