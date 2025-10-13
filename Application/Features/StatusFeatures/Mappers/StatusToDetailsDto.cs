namespace Application.Features.StatusFeatures.Mappers;

using Application.Common.DTOs.Book;
using Application.Common.DTOs.Status;

public class StatusToDetailsDto : IMapper<Status, StatusDetailsDto>
{
    private readonly IMapper<Book, BookDto> _mapper;

    public StatusToDetailsDto(IMapper<Book, BookDto> mapper)
    {
        _mapper = mapper;
    }
    public StatusDetailsDto Map(Status source)
    {
        return new StatusDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = _mapper.Map(source.Books).ToList()
        };
    }

    public IEnumerable<StatusDetailsDto> Map(IEnumerable<Status> source)
    {
        return source.Select(Map);
    }

    public void Map(Status source, StatusDetailsDto destination)
    {
        throw new NotImplementedException();
    }
}