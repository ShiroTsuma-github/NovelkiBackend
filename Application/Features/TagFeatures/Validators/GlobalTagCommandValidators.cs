namespace Application.Features.TagFeatures.Validators;

using Commands;

public sealed class CreateGlobalTagCommandValidator : AbstractValidator<CreateGlobalTagCommand>
{
    public CreateGlobalTagCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateGlobalTagCommandValidator : AbstractValidator<UpdateGlobalTagCommand>
{
    public UpdateGlobalTagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
