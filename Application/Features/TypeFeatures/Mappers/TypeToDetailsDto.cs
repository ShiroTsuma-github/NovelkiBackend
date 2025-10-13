namespace Application.Features.TypeFeatures.Mappers;

using Application.Common.DTOs.Book;
using Application.Common.DTOs.Type;
using Type = Domain.Entities.Type;

public class TypeToDetailsDto : IMapper<Type, TypeDetailsDto>
{
    private readonly IMapper<Book, BookDto> _mapper;

    public TypeToDetailsDto(IMapper<Book, BookDto> mapper)
    {
        _mapper = mapper;
    }
    public TypeDetailsDto Map(Type source)
    {
        return new TypeDetailsDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Books = _mapper.Map(source.Books).ToList()
        };
    }

    public IEnumerable<TypeDetailsDto> Map(IEnumerable<Type> source)
    {
        return source.Select(Map);
    }

    public void Map(Type source, TypeDetailsDto destination)
    {
        throw new NotImplementedException();
    }
}