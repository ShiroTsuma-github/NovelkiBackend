namespace Application.Features.TypeFeatures.Validators;

using Application.Features.TypeFeatures.Commands;

public class CreateTypeCommandValidator : AbstractValidator<CreateTypeCommand>
{
    public CreateTypeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
