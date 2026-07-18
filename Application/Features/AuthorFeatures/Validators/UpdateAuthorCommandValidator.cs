namespace Application.Features.AuthorFeatures.Validators;

using Commands;

public sealed class UpdateAuthorCommandValidator : AbstractValidator<UpdateAuthorCommand>
{
    public UpdateAuthorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.OtherNames).NotNull().Must(names => names.Count <= 25)
            .WithMessage("No more than 25 alternative names can be added.");
        RuleForEach(x => x.OtherNames).NotEmpty().MaximumLength(300);
    }
}
