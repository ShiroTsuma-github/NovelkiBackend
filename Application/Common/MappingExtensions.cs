namespace Application.Common;

using Application.Common.DTOs.Book;
using Application.Common.DTOs.Author;
using Application.Common.DTOs.Genre;
using Application.Common.DTOs.Status;
using Application.Common.DTOs.Tag;
using Application.Common.DTOs.Type;
using Application.Features.BookFeatures.Commands;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.TypeFeatures.Commands;

public static class MappingExtensions
{
    public static string NormalizeName(string value) => CollapseWhitespace(value).ToUpperInvariant();

    public static string CollapseWhitespace(string value)
    {
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeSlug(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    public static Genre ToEntity(this CreateGenreCommand source)
    {
        return new Genre
        {
            Name = source.Name,
            NormalizedName = NormalizeName(source.Name),
            Description = source.Description
        };
    }

    public static AuthorDto ToDto(this Author source)
    {
        return new AuthorDto
        {
            Id = source.Id,
            PrimaryName = source.PrimaryName,
            OtherNames = source.Names.Where(n => !n.IsPrimary).Select(n => n.Name).ToList()
        };
    }

    public static TagDto ToDto(this Tag source)
    {
        return new TagDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Color = source.Color
        };
    }

    public static void ApplyTo(this UpdateGenreCommand source, Genre destination)
    {
        destination.Name = source.Name;
        destination.NormalizedName = NormalizeName(source.Name);
        destination.Description = source.Description;
    }

    public static GenreDto ToDto(this Genre source)
    {
        return new GenreDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public static GenreDetailsDto ToDetailsDto(this Genre source)
    {
        return new GenreDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = source.BookGenres.Select(bg => bg.Book.ToDto()).ToList()
        };
    }

    public static Status ToEntity(this CreateStatusCommand source)
    {
        return new Status
        {
            Name = source.Name,
            Slug = NormalizeSlug(source.Name),
            Description = source.Description
        };
    }

    public static void ApplyTo(this UpdateStatusCommand source, Status destination)
    {
        destination.Name = source.Name;
        destination.Slug = NormalizeSlug(source.Name);
        destination.Description = source.Description;
    }

    public static StatusDto ToDto(this Status source)
    {
        return new StatusDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public static StatusDetailsDto ToDetailsDto(this Status source)
    {
        return new StatusDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = source.Books.Select(b => b.ToDto()).ToList()
        };
    }

    public static ContentType ToEntity(this CreateTypeCommand source)
    {
        return new ContentType
        {
            Name = source.Name,
            Slug = NormalizeSlug(source.Name),
            Description = source.Description
        };
    }

    public static void ApplyTo(this UpdateTypeCommand source, ContentType destination)
    {
        destination.Name = source.Name;
        destination.Slug = NormalizeSlug(source.Name);
        destination.Description = source.Description;
    }

    public static TypeDto ToDto(this ContentType source)
    {
        return new TypeDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public static TypeDetailsDto ToDetailsDto(this ContentType source)
    {
        return new TypeDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = source.Books.Select(b => b.ToDto()).ToList()
        };
    }

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

    public static BookCoverDto ToDto(this BookCover source, Guid bookId)
    {
        var version = GetCoverVersion(source).ToUnixTimeMilliseconds();
        return new BookCoverDto
        {
            Id = source.Id,
            Status = source.Status.ToString(),
            Source = source.Source?.ToString(),
            ImageUrl = source.StoragePath == null ? null : $"/api/v1/book/{bookId}/cover/file?v={version}",
            ThumbnailImageUrl = source.ThumbnailStoragePath == null ? null : $"/api/v1/book/{bookId}/cover/thumbnail?v={version}",
            OriginalImageUrl = source.OriginalImageUrl,
            MimeType = source.MimeType,
            SizeBytes = source.SizeBytes,
            Width = source.Width,
            Height = source.Height,
            ThumbnailMimeType = source.ThumbnailMimeType,
            ThumbnailSizeBytes = source.ThumbnailSizeBytes,
            ThumbnailWidth = source.ThumbnailWidth,
            ThumbnailHeight = source.ThumbnailHeight,
            FailureReason = source.FailureReason,
            LastAttemptAt = source.LastAttemptAt
        };
    }

    private static DateTimeOffset GetCoverVersion(BookCover source)
    {
        if (source.LastAttemptAt.HasValue)
        {
            return source.LastAttemptAt.Value;
        }

        if (source.LastModified != default)
        {
            return source.LastModified;
        }

        return source.Created != default ? source.Created : DateTimeOffset.UnixEpoch;
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
}
