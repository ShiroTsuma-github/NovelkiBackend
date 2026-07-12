namespace Application.Features.BookFeatures.Queries.GetBook;

using Application.Common;
using Application.Common.DTOs.Book;
using Application.Common.Interfaces;

public sealed record GetBookSummaryQuery(string? Query = null) : IRequest<BookSummaryDto>;

public sealed class GetBookSummaryHandler : IRequestHandler<GetBookSummaryQuery, BookSummaryDto>
{
    private readonly IBookRepository _repository;
    private readonly IUser _user;

    public GetBookSummaryHandler(IBookRepository repository, IUser user)
    {
        _repository = repository;
        _user = user;
    }

    public async Task<BookSummaryDto> Handle(GetBookSummaryQuery request, CancellationToken cancellationToken)
    {
        var criteria = BookSearchQueryParser.Parse(request.Query);
        var summary = await _repository.GetSummaryAsync(_user.RequiredId, criteria, cancellationToken);

        return new BookSummaryDto
        {
            TotalBooks = summary.TotalBooks,
            RatedBooks = summary.RatedBooks,
            UnratedBooks = summary.TotalBooks - summary.RatedBooks,
            AverageRating = summary.AverageRating,
            CurrentChapters = summary.CurrentChapters,
            BooksWithKnownCurrentChapter = summary.BooksWithKnownCurrentChapter,
            BooksWithoutKnownCurrentChapter = summary.TotalBooks - summary.BooksWithKnownCurrentChapter,
            StatusCounts = summary.StatusCounts
                .Select(status => new BookSummaryStatusCountDto
                {
                    Status = status.Status,
                    Count = status.Count,
                })
                .ToList(),
            TypeCounts = summary.TypeCounts
                .Select(type => new BookSummaryTypeCountDto
                {
                    Type = type.Type,
                    BookCount = type.BookCount,
                    CurrentChapters = type.CurrentChapters,
                })
                .ToList(),
            GenreCounts = summary.GenreCounts
                .Select(genre => new BookSummaryGenreCountDto
                {
                    Genre = genre.Genre,
                    BookCount = genre.BookCount,
                })
                .ToList(),
            RatingCounts = summary.RatingCounts
                .Select(rating => new BookSummaryRatingCountDto
                {
                    Rating = rating.Rating,
                    BookCount = rating.BookCount,
                })
                .ToList(),
        };
    }
}
