namespace Application.Features.BookFeatures.Mappers;

using Application.Common.DTOs.Book;

public class BookToDto : IMapper<Book, BookDto>
{
    public BookDto Map(Book source)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<BookDto> Map(IEnumerable<Book> source)
    {
        throw new NotImplementedException();
    }

    public void Map(Book source, BookDto destination)
    {
        throw new NotImplementedException();
    }
}
