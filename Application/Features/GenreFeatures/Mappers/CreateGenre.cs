namespace Application.Features.GenreFeatures.Mappers;

using Application.Features.GenreFeatures.Commands;

public class CreateGenreMapper : IMapper<CreateGenreCommand, Genre>
{
    public Genre Map(CreateGenreCommand source)
    {
        return new Genre
        { 
            Name = source.Name,
            Description = source.Description
        };
    }

    public IEnumerable<Genre> Map(IEnumerable<CreateGenreCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(CreateGenreCommand source, Genre destination)
    {
        throw new NotImplementedException();
    }
}
