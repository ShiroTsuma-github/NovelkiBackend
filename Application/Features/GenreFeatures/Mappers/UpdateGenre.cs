namespace Application.Features.GenreFeatures.Mappers;

using Application.Features.GenreFeatures.Commands;

public class UpdateGenreMapper : IMapper<UpdateGenreCommand, Genre>
{
    public void Map(UpdateGenreCommand source, Genre destination)
    {
        destination.Name = source.Name;
        destination.Description = source.Description;
    }

    public IEnumerable<Genre> Map(IEnumerable<UpdateGenreCommand> source)
    {
        throw new NotImplementedException();
    }

    public Genre Map(UpdateGenreCommand source)
    {
        throw new NotImplementedException();
    }
}
