namespace Application.Features.TypeFeatures.Mappers;

using Application.Common.DTOs.Type;
using Type = Domain.Entities.Type;

public class TypeToDto : IMapper<Type, TypeDto>
{
    public TypeDto Map(Type source)
    {
        return new TypeDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<TypeDto> Map(IEnumerable<Type> source)
    {
        return source.Select(Map);
    }

    public void Map(Type source, TypeDto destination)
    {
        throw new NotImplementedException();
    }
}
