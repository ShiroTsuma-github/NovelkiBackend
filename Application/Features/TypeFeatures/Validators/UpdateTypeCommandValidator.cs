namespace Application.Features.TypeFeatures.Validators;

using Application.Features.TypeFeatures.Commands;

public class UpdateTypeCommandValidator : AbstractValidator<UpdateTypeCommand>
{
    public UpdateTypeCommandValidator()
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
