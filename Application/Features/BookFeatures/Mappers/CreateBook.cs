namespace Application.Features.BookFeatures.Mappers;

using Application.Features.BookFeatures.Commands;

public class CreateBookMapper : IMapper<CreateBookCommand, Book>
{
    public Book Map(CreateBookCommand source)
    {
        return new Book
        {
            Title = source.Title,
            //Author = source.Author,
        };
    }

    public IEnumerable<Book> Map(IEnumerable<CreateBookCommand> source)
    {
        throw new NotImplementedException();
    }

    public void Map(CreateBookCommand source, Book destination)
    {
        throw new NotImplementedException();
    }
}
