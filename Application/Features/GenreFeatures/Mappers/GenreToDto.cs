namespace Application.Features.GenreFeatures.Mappers;

using Application.Common.DTOs.Genre;

public class GenreToDto : IMapper<Genre, GenreDto>
{
    public GenreDto Map(Genre source)
    {
        return new GenreDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<GenreDto> Map(IEnumerable<Genre> source)
    {
        return source.Select(Map);
    }

    public void Map(Genre source, GenreDto destination)
    {
        throw new NotImplementedException();
    }
}
