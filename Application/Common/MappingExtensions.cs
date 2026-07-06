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
    public static string NormalizeName(string value) => value.Trim().ToUpperInvariant();

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
            Comment = source.Comment,
            Notes = source.Notes,
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
            Comment = source.Comment,
            Notes = source.Notes,
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

    public static BookTitle ToPrimaryTitle(this string title)
    {
        return new BookTitle
        {
            Title = title,
            NormalizedTitle = NormalizeName(title),
            IsPrimary = true,
            Source = "Manual"
        };
    }

    public static BookTitle ToBookTitle(this BookTitleInput input)
    {
        return new BookTitle
        {
            Title = input.Title,
            NormalizedTitle = NormalizeName(input.Title),
            Language = input.Language,
            IsPrimary = false,
            Source = input.Source ?? "Manual"
        };
    }

    public static BookLink ToBookLink(this BookLinkInput input)
    {
        return new BookLink
        {
            Url = input.Url,
            Label = input.Label,
            SourceType = input.SourceType,
            IsPrimary = input.IsPrimary,
            LastReadHere = input.LastReadHere
        };
    }
}
