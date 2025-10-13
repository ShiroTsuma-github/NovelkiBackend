namespace Application.Features.StatusFeatures.Mappers;

using Application.Features.StatusFeatures.Commands;

public class CreateStatusMapper : IMapper<CreateStatusCommand, Status>
{
    public Status Map(CreateStatusCommand source)
    {
        return new Status
        { 
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<Status> Map(IEnumerable<CreateStatusCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(CreateStatusCommand source, Status destination)
    {
        throw new NotImplementedException();
    }
}
