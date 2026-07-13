namespace Application.Common;

using Application.Common.DTOs.Book;
using Domain.Models;

public static partial class MappingExtensions
{
    public static BookSummaryDto ToDto(this BookSummarySnapshot source)
    {
        return new BookSummaryDto
        {
            TotalBooks = source.TotalBooks,
            RatedBooks = source.RatedBooks,
            UnratedBooks = source.TotalBooks - source.RatedBooks,
            AverageRating = source.AverageRating,
            CurrentChapters = source.CurrentChapters,
            BooksWithKnownCurrentChapter = source.BooksWithKnownCurrentChapter,
            BooksWithoutKnownCurrentChapter = source.TotalBooks - source.BooksWithKnownCurrentChapter,
            StatusCounts = source.StatusCounts.Select(item => new BookSummaryStatusCountDto { Status = item.Status, Count = item.Count }).ToList(),
            TypeCounts = source.TypeCounts.Select(item => new BookSummaryTypeCountDto { Type = item.Type, BookCount = item.BookCount, CurrentChapters = item.CurrentChapters }).ToList(),
            GenreCounts = source.GenreCounts.Select(item => new BookSummaryGenreCountDto { Genre = item.Genre, BookCount = item.BookCount }).ToList(),
            RatingCounts = source.RatingCounts.Select(item => new BookSummaryRatingCountDto { Rating = item.Rating, BookCount = item.BookCount }).ToList(),
        };
    }
}
