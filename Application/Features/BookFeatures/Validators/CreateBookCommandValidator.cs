namespace Application.Features.BookFeatures.Validators;

using Application.Common.DTOs.Book;
using Commands;
using System.Linq.Expressions;

public class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        Include(new BookCommandValidatorRules<CreateBookCommand>(
            x => x.PrimaryTitle,
            x => x.AuthorName,
            x => x.AlternativeTitles,
            x => x.Tags,
            x => x.TotalChapters,
            x => x.CurrentChapterNumber,
            x => x.CurrentChapterLabel,
            x => x.Rating,
            x => x.Priority,
            x => x.Description,
            x => x.Notes,
            x => x.RawImportedLine,
            x => x.Links));
    }
}

public class UpdateBookCommandValidator : AbstractValidator<UpdateBookCommand>
{
    public UpdateBookCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        Include(new BookCommandValidatorRules<UpdateBookCommand>(
            x => x.PrimaryTitle,
            x => x.AuthorName,
            x => x.AlternativeTitles,
            x => x.Tags,
            x => x.TotalChapters,
            x => x.CurrentChapterNumber,
            x => x.CurrentChapterLabel,
            x => x.Rating,
            x => x.Priority,
            x => x.Description,
            x => x.Notes,
            x => x.RawImportedLine,
            x => x.Links));
    }
}

public class UpdateBookProgressCommandValidator : AbstractValidator<UpdateBookProgressCommand>
{
    public UpdateBookProgressCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.CurrentChapterNumber)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CurrentChapterNumber.HasValue);

        RuleFor(x => x.CurrentChapterLabel)
            .MaximumLength(100)
            .WithMessage("Chapter label must be 100 characters or fewer.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .WithMessage("Comment must be 1000 characters or fewer.");
    }
}

internal sealed class BookCommandValidatorRules<TCommand> : AbstractValidator<TCommand>
{
    public BookCommandValidatorRules(
        Expression<Func<TCommand, string>> primaryTitle,
        Expression<Func<TCommand, string?>> authorName,
        Expression<Func<TCommand, IEnumerable<BookTitleInput>?>> alternativeTitles,
        Expression<Func<TCommand, IEnumerable<string>?>> tags,
        Expression<Func<TCommand, decimal?>> totalChapters,
        Expression<Func<TCommand, decimal?>> currentChapterNumber,
        Expression<Func<TCommand, string?>> currentChapterLabel,
        Expression<Func<TCommand, int?>> rating,
        Expression<Func<TCommand, int?>> priority,
        Expression<Func<TCommand, string?>> description,
        Expression<Func<TCommand, string?>> notes,
        Expression<Func<TCommand, string?>> rawImportedLine,
        Expression<Func<TCommand, IEnumerable<BookLinkInput>?>> links)
    {
        var totalChaptersValue = totalChapters.Compile();
        var currentChapterNumberValue = currentChapterNumber.Compile();
        var ratingValue = rating.Compile();
        var priorityValue = priority.Compile();
        var alternativeTitlesValue = alternativeTitles.Compile();
        var tagsValue = tags.Compile();
        var linksValue = links.Compile();

        RuleFor(primaryTitle)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Title is required.")
            .Must(value => value == null || value.Trim().Length <= 500)
            .WithMessage("Title must be 500 characters or fewer.");

        RuleFor(authorName)
            .MaximumLength(300);

        RuleFor(totalChapters)
            .GreaterThan(0)
            .WithMessage("Total chapters must be greater than 0.")
            .When(x => totalChaptersValue(x).HasValue);

        RuleFor(currentChapterNumber)
            .GreaterThanOrEqualTo(0)
            .When(x => currentChapterNumberValue(x).HasValue);

        RuleFor(x => x)
            .Must(x => currentChapterNumberValue(x) <= totalChaptersValue(x))
            .When(x => currentChapterNumberValue(x).HasValue && totalChaptersValue(x).HasValue)
            .WithName("CurrentChapterNumber")
            .WithMessage("Current chapter cannot be greater than total chapters.");

        RuleFor(currentChapterLabel)
            .MaximumLength(100);

        RuleFor(rating)
            .InclusiveBetween(1, 10)
            .When(x => ratingValue(x).HasValue);

        RuleFor(priority)
            .InclusiveBetween(1, 5)
            .When(x => priorityValue(x).HasValue);

        RuleFor(description)
            .MaximumLength(4000);

        RuleFor(notes)
            .MaximumLength(4000);

        RuleFor(rawImportedLine)
            .MaximumLength(4000);

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                var index = 0;
                foreach (var title in alternativeTitlesValue(command) ?? Enumerable.Empty<BookTitleInput>())
                {
                    if (string.IsNullOrWhiteSpace(title.Title))
                    {
                        context.AddFailure($"AlternativeTitles[{index}].Title", "Title must not be empty.");
                    }
                    else if (title.Title.Length > 500)
                    {
                        context.AddFailure($"AlternativeTitles[{index}].Title",
                            "Title must be 500 characters or fewer.");
                    }

                    if (title.Language?.Length > 10)
                    {
                        context.AddFailure($"AlternativeTitles[{index}].Language",
                            "Language must be 10 characters or fewer.");
                    }

                    if (title.Source?.Length > 50)
                    {
                        context.AddFailure($"AlternativeTitles[{index}].Source",
                            "Source must be 50 characters or fewer.");
                    }

                    index++;
                }
            });

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                var index = 0;
                foreach (var tag in tagsValue(command) ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        context.AddFailure($"Tags[{index}]", "Tag must not be empty.");
                    }
                    else if (tag.Length > 100)
                    {
                        context.AddFailure($"Tags[{index}]", "Tag must be 100 characters or fewer.");
                    }

                    index++;
                }
            });

        RuleFor(x => x)
            .Custom((command, context) =>
            {
                var index = 0;
                foreach (var link in linksValue(command) ?? Enumerable.Empty<BookLinkInput>())
                {
                    if (string.IsNullOrWhiteSpace(link.Url))
                    {
                        context.AddFailure($"Links[{index}].Url", "Url must not be empty.");
                    }
                    else if (link.Url.Length > 2000)
                    {
                        context.AddFailure($"Links[{index}].Url", "Url must be 2000 characters or fewer.");
                    }
                    else if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var parsed) ||
                             (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                    {
                        context.AddFailure($"Links[{index}].Url", "Url must be an absolute HTTP or HTTPS URL.");
                    }

                    if (link.Label?.Length > 200)
                    {
                        context.AddFailure($"Links[{index}].Label", "Label must be 200 characters or fewer.");
                    }

                    if (string.IsNullOrWhiteSpace(link.SourceType))
                    {
                        context.AddFailure($"Links[{index}].SourceType", "Source type must not be empty.");
                    }
                    else if (link.SourceType.Length > 50)
                    {
                        context.AddFailure($"Links[{index}].SourceType", "Source type must be 50 characters or fewer.");
                    }

                    index++;
                }
            });
    }
}
