namespace Application.Features.GenreFeatures.Mappers;

using Application.Common.DTOs.Book;
using Application.Common.DTOs.Genre;

public class GenreToDetailsDto : IMapper<Genre, GenreDetailsDto>
{
    private readonly IMapper<Book, BookDto> _mapper;

    public GenreToDetailsDto(IMapper<Book, BookDto> mapper)
    {
        _mapper = mapper;
    }
    public GenreDetailsDto Map(Genre source)
    {
        return new GenreDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = _mapper.Map(source.Books).ToList()
        };
    }

    public IEnumerable<GenreDetailsDto> Map(IEnumerable<Genre> source)
    {
        return source.Select(Map);
    }

    public void Map(Genre source, GenreDetailsDto destination)
    {
        throw new NotImplementedException();
    }
}
