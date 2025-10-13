namespace Application.Features.GenreFeatures.Validators;

using Application.Features.GenreFeatures.Commands;

public class CreateGenreCommandValidator : AbstractValidator<CreateGenreCommand>
{
    public CreateGenreCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
