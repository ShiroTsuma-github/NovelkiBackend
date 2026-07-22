namespace Application.Features.BookFeatures.Queries;

using Common.DTOs.Book;

public sealed record ParseBookHtmlQuery(string Html) : IRequest<BookHtmlParseResult>;

public sealed class ParseBookHtmlQueryValidator : AbstractValidator<ParseBookHtmlQuery>
{
    public const int MaxHtmlCharacters = 8 * 1024 * 1024;

    public ParseBookHtmlQueryValidator()
    {
        RuleFor(query => query.Html)
            .NotEmpty()
            .WithMessage("HTML is required.")
            .MaximumLength(MaxHtmlCharacters)
            .WithMessage($"HTML cannot exceed {MaxHtmlCharacters} characters.");
    }
}

public sealed class ParseBookHtmlQueryHandler : IRequestHandler<ParseBookHtmlQuery, BookHtmlParseResult>
{
    private readonly IBookHtmlParser _parser;

    public ParseBookHtmlQueryHandler(IBookHtmlParser parser)
    {
        _parser = parser;
    }

    public Task<BookHtmlParseResult> Handle(ParseBookHtmlQuery request, CancellationToken cancellationToken)
    {
        return _parser.ParseAsync(request.Html, cancellationToken);
    }
}
