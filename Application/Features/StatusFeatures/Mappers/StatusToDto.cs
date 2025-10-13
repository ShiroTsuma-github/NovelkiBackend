namespace Application.Features.StatusFeatures.Mappers;

using Application.Common.DTOs.Status;

public class StatusToDto : IMapper<Status, StatusDto>
{
    public StatusDto Map(Status source)
    {
        return new StatusDto
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<StatusDto> Map(IEnumerable<Status> source)
    {
        return source.Select(Map);
    }

    public void Map(Status source, StatusDto destination)
    {
        throw new NotImplementedException();
    }
}
