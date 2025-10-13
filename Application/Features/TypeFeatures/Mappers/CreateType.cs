namespace Application.Features.TypeFeatures.Mappers;

using Application.Features.TypeFeatures.Commands;
using Type = Domain.Entities.Type;

public class CreateTypeMapper : IMapper<CreateTypeCommand, Type>
{
    public Type Map(CreateTypeCommand source)
    {
        return new Type
        { 
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<Type> Map(IEnumerable<CreateTypeCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(CreateTypeCommand source, Type destination)
    {
        throw new NotImplementedException();
    }
}
