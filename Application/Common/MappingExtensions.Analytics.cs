namespace Application.Common;

using Application.Common.DTOs.Book;
using Domain.Models;

public static partial class MappingExtensions
{
    public static BookAnalyticsDto ToDto(this BookAnalyticsSnapshot source)
    {
        return new BookAnalyticsDto
        {
            GeneratedAt = source.GeneratedAt,
            Scope = new BookAnalyticsScopeDto
            {
                Query = source.Scope.Query,
                From = source.Scope.From,
                To = source.Scope.To,
                Bucket = source.Scope.Bucket
            },
            Overview = new BookAnalyticsOverviewDto
            {
                TotalBooks = source.Overview.TotalBooks,
                RatedBooks = source.Overview.RatedBooks,
                UnratedBooks = source.Overview.UnratedBooks,
                AverageRating = source.Overview.AverageRating,
                CurrentChapters = source.Overview.CurrentChapters,
                BooksWithKnownCurrentChapter = source.Overview.BooksWithKnownCurrentChapter,
                BooksWithoutKnownCurrentChapter = source.Overview.BooksWithoutKnownCurrentChapter
            },
            Composition = new BookAnalyticsCompositionDto
            {
                StatusByType = source.Composition.StatusByType
                    .Select(item => new BookAnalyticsStatusByTypeDto
                    {
                        Type = item.Type,
                        TotalBooks = item.TotalBooks,
                        Statuses = item.Statuses
                            .Select(status => new BookAnalyticsStatusCountDto
                            {
                                Status = status.Status,
                                BookCount = status.BookCount
                            })
                            .ToList()
                    })
                    .ToList(),
                Genres = source.Composition.Genres
                    .Select(item => new BookAnalyticsRelationCountDto
                    {
                        Name = item.Name,
                        BookCount = item.BookCount,
                        ShareOfBooks = item.ShareOfBooks
                    })
                    .ToList(),
                Tags = source.Composition.Tags
                    .Select(item => new BookAnalyticsRelationCountDto
                    {
                        Name = item.Name,
                        BookCount = item.BookCount,
                        ShareOfBooks = item.ShareOfBooks
                    })
                    .ToList()
            },
            Ratings = new BookAnalyticsRatingsDto
            {
                RatedBooks = source.Ratings.RatedBooks,
                UnratedBooks = source.Ratings.UnratedBooks,
                AverageRating = source.Ratings.AverageRating,
                Counts = source.Ratings.Counts
                    .Select(item => new BookAnalyticsRatingCountDto
                    {
                        Rating = item.Rating,
                        BookCount = item.BookCount
                    })
                    .ToList()
            },
            Planning = new BookAnalyticsPlanningDto
            {
                PrioritiesByStatus = source.Planning.PrioritiesByStatus
                    .Select(item => new BookAnalyticsPrioritiesByStatusDto
                    {
                        Status = item.Status,
                        TotalBooks = item.TotalBooks,
                        Priorities = item.Priorities
                            .Select(priority => new BookAnalyticsPriorityCountDto
                            {
                                Priority = priority.Priority,
                                BookCount = priority.BookCount
                            })
                            .ToList()
                    })
                    .ToList()
            },
            Progress = new BookAnalyticsProgressDto
            {
                TypeVolumes = source.Progress.TypeVolumes
                    .Select(item => new BookAnalyticsTypeVolumeDto
                    {
                        Type = item.Type,
                        BookCount = item.BookCount,
                        CurrentChapters = item.CurrentChapters,
                        AverageCurrentChapter = item.AverageCurrentChapter,
                        MedianCurrentChapter = item.MedianCurrentChapter
                    })
                    .ToList()
            },
            Activity = new BookAnalyticsActivityDto
            {
                Points = source.Activity.Points
                    .Select(item => new BookAnalyticsActivityPointDto
                    {
                        Date = item.Date,
                        ProgressEvents = item.ProgressEvents,
                        BooksTouched = item.BooksTouched,
                        ChaptersAdvanced = item.ChaptersAdvanced
                    })
                    .ToList()
            }
        };
    }
}
