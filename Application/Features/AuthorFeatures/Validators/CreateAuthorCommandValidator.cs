namespace Application.Features.AuthorFeatures.Validators;

using Commands;

public sealed class CreateAuthorCommandValidator : AbstractValidator<CreateAuthorCommand>
{
    public CreateAuthorCommandValidator()
    {
        RuleFor(x => x.PrimaryName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.OtherNames).Must(names => names is null || names.Count <= 25)
            .WithMessage("No more than 25 alternative names can be added.");
        RuleForEach(x => x.OtherNames).NotEmpty().MaximumLength(300);
    }
}
