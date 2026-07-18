namespace Application.Features.TagFeatures.Validators;

using Commands;

public sealed class UpdateTagCommandValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
