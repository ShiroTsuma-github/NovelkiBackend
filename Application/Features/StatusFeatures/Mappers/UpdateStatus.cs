namespace Application.Features.StatusFeatures.Mappers;

using Application.Features.StatusFeatures.Commands;

public class UpdateStatusMapper : IMapper<UpdateStatusCommand, Status>
{
    public Status Map(UpdateStatusCommand source)
    {
        return new Status
        {
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<Status> Map(IEnumerable<UpdateStatusCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(UpdateStatusCommand source, Status destination)
    {
        destination.Name = source.Name;
        destination.Description = source.Description;
    }
}
