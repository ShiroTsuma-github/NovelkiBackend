namespace Application.Features.TypeFeatures.Mappers;

using Application.Features.TypeFeatures.Commands;
using Type = Domain.Entities.Type;

public class UpdateTypeMapper : IMapper<UpdateTypeCommand, Type>
{
    public Type Map(UpdateTypeCommand source)
    {
        return new Type
        {
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<Type> Map(IEnumerable<UpdateTypeCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(UpdateTypeCommand source, Type destination)
    {
        destination.Name = source.Name;
        destination.Description = source.Description;
    }
}
