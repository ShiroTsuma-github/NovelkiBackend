namespace Application.Common;

using Application.Common.DTOs.Book;
using Application.Features.BookFeatures.Commands;

public static partial class MappingExtensions
{
    public static BookDto ToDto(this Book source)
    {
        return new BookDto
        {
            Id = source.Id,
            Created = source.Created,
            LastModified = source.LastModified,
            PrimaryTitle = source.PrimaryTitle,
            Description = source.Description,
            AlternativeTitles = source.Titles.Where(t => !t.IsPrimary).Select(t => t.Title).ToList(),
            AuthorId = source.AuthorId,
            Author = source.Author?.PrimaryName,
            ContentType = source.ContentType.Name,
            Status = source.Status.Name,
            CurrentChapterNumber = source.CurrentChapterNumber,
            CurrentChapterLabel = source.CurrentChapterLabel,
            TotalChapters = source.TotalChapters,
            Rating = source.Rating,
            Priority = source.Priority,
            Notes = source.Notes,
            ProgressHistory = source.ProgressHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new BookProgressHistoryDto
                {
                    Id = h.Id,
                    ChangedAt = h.ChangedAt,
                    ChapterNumber = h.ChapterNumber,
                    ChapterLabel = h.ChapterLabel,
                    Comment = h.Comment
                })
                .ToList(),
            Cover = source.Cover?.ToDto(source.Id),
            Genres = source.BookGenres.Select(bg => bg.Genre.Name).ToList(),
            Tags = source.BookTags.Select(bt => bt.Tag.Name).ToList(),
            Links = source.Links.Select(l => new BookLinkDto
            {
                Id = l.Id,
                Url = l.Url,
                Label = l.Label,
                SourceType = l.SourceType,
                IsPrimary = l.IsPrimary,
                LastReadHere = l.LastReadHere
            }).ToList()
        };
    }

    public static BookListItemDto ToListItemDto(this Book source)
    {
        var alternativeTitles = source.Titles.Where(t => !t.IsPrimary).Select(t => t.Title).ToList();
        var genres = source.BookGenres.Select(bg => bg.Genre.Name).ToList();
        var tags = source.BookTags.Select(bt => bt.Tag.Name).ToList();

        return new BookListItemDto
        {
            Id = source.Id,
            Created = source.Created,
            LastModified = source.LastModified,
            PrimaryTitle = source.PrimaryTitle,
            Description = CreateExcerpt(source.Description),
            AlternativeTitles = alternativeTitles.Take(4).ToList(),
            AlternativeTitlesCount = alternativeTitles.Count,
            Author = source.Author?.PrimaryName,
            ContentType = source.ContentType.Name,
            Status = source.Status.Name,
            CurrentChapterNumber = source.CurrentChapterNumber,
            CurrentChapterLabel = source.CurrentChapterLabel,
            TotalChapters = source.TotalChapters,
            Rating = source.Rating,
            Priority = source.Priority,
            Notes = CreateExcerpt(source.Notes),
            Cover = source.Cover?.ToDto(source.Id),
            Genres = genres.Take(4).ToList(),
            GenresCount = genres.Count,
            Tags = tags.Take(4).ToList(),
            TagsCount = tags.Count,
            LinksCount = source.Links.Count
        };
    }

    public static AdminBookDto ToAdminDto(this Book source)
    {
        return new AdminBookDto
        {
            Id = source.Id,
            Created = source.Created,
            LastModified = source.LastModified,
            OwnerId = source.OwnerId,
            PrimaryTitle = source.PrimaryTitle,
            Description = source.Description,
            AlternativeTitles = source.Titles.Where(t => !t.IsPrimary).Select(t => t.Title).ToList(),
            AuthorId = source.AuthorId,
            Author = source.Author?.PrimaryName,
            ContentType = source.ContentType.Name,
            Status = source.Status.Name,
            CurrentChapterNumber = source.CurrentChapterNumber,
            CurrentChapterLabel = source.CurrentChapterLabel,
            TotalChapters = source.TotalChapters,
            Rating = source.Rating,
            Priority = source.Priority,
            Notes = source.Notes,
            ProgressHistory = source.ProgressHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new BookProgressHistoryDto
                {
                    Id = h.Id,
                    ChangedAt = h.ChangedAt,
                    ChapterNumber = h.ChapterNumber,
                    ChapterLabel = h.ChapterLabel,
                    Comment = h.Comment
                })
                .ToList(),
            Cover = source.Cover?.ToDto(source.Id),
            Genres = source.BookGenres.Select(bg => bg.Genre.Name).ToList(),
            Tags = source.BookTags.Select(bt => bt.Tag.Name).ToList(),
            Links = source.Links.Select(l => new BookLinkDto
            {
                Id = l.Id,
                Url = l.Url,
                Label = l.Label,
                SourceType = l.SourceType,
                IsPrimary = l.IsPrimary,
                LastReadHere = l.LastReadHere
            }).ToList()
        };
    }

    public static AdminBookListItemDto ToAdminListItemDto(this Book source, string? ownerUsername, string? ownerEmail)
    {
        var dto = source.ToListItemDto();
        return new AdminBookListItemDto
        {
            Id = dto.Id,
            Created = dto.Created,
            LastModified = dto.LastModified,
            PrimaryTitle = dto.PrimaryTitle,
            Description = dto.Description,
            AlternativeTitles = dto.AlternativeTitles,
            AlternativeTitlesCount = dto.AlternativeTitlesCount,
            Author = dto.Author,
            ContentType = dto.ContentType,
            Status = dto.Status,
            CurrentChapterNumber = dto.CurrentChapterNumber,
            CurrentChapterLabel = dto.CurrentChapterLabel,
            TotalChapters = dto.TotalChapters,
            Rating = dto.Rating,
            Priority = dto.Priority,
            Notes = dto.Notes,
            Cover = dto.Cover,
            Genres = dto.Genres,
            GenresCount = dto.GenresCount,
            Tags = dto.Tags,
            TagsCount = dto.TagsCount,
            LinksCount = dto.LinksCount,
            OwnerId = source.OwnerId,
            OwnerUsername = ownerUsername,
            OwnerEmail = ownerEmail
        };
    }

    public static BookTitle ToPrimaryTitle(this string title)
    {
        var trimmedTitle = title.Trim();
        return new BookTitle
        {
            Title = trimmedTitle,
            NormalizedTitle = NormalizeName(trimmedTitle),
            IsPrimary = true,
            Source = "Manual"
        };
    }

    public static BookTitle ToBookTitle(this BookTitleInput input)
    {
        var trimmedTitle = input.Title.Trim();
        return new BookTitle
        {
            Title = trimmedTitle,
            NormalizedTitle = NormalizeName(trimmedTitle),
            Language = string.IsNullOrWhiteSpace(input.Language) ? null : input.Language.Trim(),
            IsPrimary = false,
            Source = string.IsNullOrWhiteSpace(input.Source) ? "Manual" : input.Source.Trim()
        };
    }

    public static BookLink ToBookLink(this BookLinkInput input)
    {
        return new BookLink
        {
            Url = input.Url.Trim(),
            Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim(),
            SourceType = input.SourceType.Trim(),
            IsPrimary = input.IsPrimary,
            LastReadHere = input.LastReadHere
        };
    }

    private static string? CreateExcerpt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var collapsed = CollapseWhitespace(value);
        return collapsed.Length > 80 ? $"{collapsed[..77]}..." : collapsed;
    }
}
