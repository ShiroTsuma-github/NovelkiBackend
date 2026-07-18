namespace Application.Common;

using DTOs.Author;
using DTOs.Book;
using DTOs.Genre;
using DTOs.Status;
using DTOs.Tag;
using DTOs.Type;
using Features.GenreFeatures.Commands;
using Features.StatusFeatures.Commands;
using Features.TypeFeatures.Commands;

public static partial class MappingExtensions
{
    public static Genre ToEntity(this CreateGenreCommand source)
    {
        return new Genre
        {
            Name = source.Name, NormalizedName = NormalizeName(source.Name), Description = source.Description
        };
    }

    public static AuthorDto ToDto(this Author source)
    {
        return new AuthorDto
        {
            Id = source.Id,
            PrimaryName = source.PrimaryName,
            OtherNames = source.Names.Where(n => !n.IsPrimary).OrderBy(n => n.Name).Select(n => n.Name).ToList(),
            IsPublic = source.IsPublic
        };
    }

    public static TagDto ToDto(this Tag source)
    {
        return new TagDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Color = source.Color,
            IsGlobal = source.IsGlobal
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
        return new GenreDto { Id = source.Id, Name = source.Name, Description = source.Description };
    }

    public static GenreDetailsDto ToDetailsDto(this Genre source)
    {
        return MapDetails(source.Id, source.Name, source.Description, source.BookGenres.Select(bg => bg.Book),
            new GenreDetailsDto { Name = source.Name });
    }

    public static Status ToEntity(this CreateStatusCommand source)
    {
        return new Status { Name = source.Name, Slug = NormalizeSlug(source.Name), Description = source.Description };
    }

    public static void ApplyTo(this UpdateStatusCommand source, Status destination)
    {
        destination.Name = source.Name;
        destination.Slug = NormalizeSlug(source.Name);
        destination.Description = source.Description;
    }

    public static StatusDto ToDto(this Status source)
    {
        return new StatusDto { Id = source.Id, Name = source.Name, Description = source.Description };
    }

    public static StatusDetailsDto ToDetailsDto(this Status source)
    {
        return MapDetails(source.Id, source.Name, source.Description, source.Books,
            new StatusDetailsDto { Name = source.Name });
    }

    public static ContentType ToEntity(this CreateTypeCommand source)
    {
        return new ContentType
        {
            Name = source.Name, Slug = NormalizeSlug(source.Name), Description = source.Description
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
        return new TypeDto { Id = source.Id, Name = source.Name, Description = source.Description };
    }

    public static TypeDetailsDto ToDetailsDto(this ContentType source)
    {
        return MapDetails(source.Id, source.Name, source.Description, source.Books,
            new TypeDetailsDto { Name = source.Name });
    }

    private static TDetailsDto MapDetails<TDetailsDto>(
        Guid id,
        string name,
        string? description,
        IEnumerable<Book> books,
        TDetailsDto destination)
        where TDetailsDto : BookCollectionDetailsDto
    {
        destination.Id = id;
        destination.Name = name;
        destination.Description = description;
        destination.Books = books.Select(book => book.ToDto()).ToList();
        return destination;
    }
}
